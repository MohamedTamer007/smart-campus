using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SmartCampus.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SmartCampus.Tests;

public sealed class CachedLostFoundReplayFactAttribute : FactAttribute
{
    public CachedLostFoundReplayFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SMART_CAMPUS_RUN_CACHED_AI_REPLAY_TESTS"), "true", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB")))
            Skip = "Requires explicit replay opt-in and an isolated test database already seeded by a real synthetic evaluation run.";
    }
}

public sealed class CachedLostFoundEvaluationTests(ITestOutputHelper output)
{
    private sealed class RejectInferenceClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException(
            $"Unexpected external client '{name}' while replaying persisted evaluation embeddings.");
    }

    [CachedLostFoundReplayFact]
    public async Task Saved_embedding_gallery_replays_through_application_ranking_without_external_calls()
    {
        var connectionString = Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB")!;
        var parsedConnection = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Contains(parsedConnection.Host, new[] { "localhost", "127.0.0.1" });
        Assert.EndsWith("_test", parsedConnection.Database, StringComparison.Ordinal);

        var password = "Local-test-" + Guid.NewGuid().ToString("N");
        var email = "evaluation-replay-" + Guid.NewGuid().ToString("N") + "@o6u.edu.eg";
        using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Campus"] = connectionString,
                ["PublicOrigin"] = "http://localhost",
                ["Email:LocalSink"] = "true",
                ["Email:SinkDirectory"] = Path.Combine(Path.GetTempPath(), "smart-campus-cached-replay", Guid.NewGuid().ToString("N")),
                ["RateLimits:AuthPerMinute"] = "1000",
                ["RateLimits:PerMinute"] = "10000"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CampusDb>>();
                services.AddDbContext<CampusDb>(options => options.UseNpgsql(connectionString,
                    postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "smart_campus")));
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory, RejectInferenceClientFactory>();
            });
        });

        using (var scope = host.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("admin"))
                Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("admin"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>();
            var account = new CampusUser { UserName = email, Email = email, DisplayName = "TEST evaluation replay", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(account, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(account, "admin")).Succeeded);
        }

        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CampusFixture.Csrf(client);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        await CampusFixture.Csrf(client);

        var preflight = await CampusFixture.Response(await client.GetAsync("/api/v1/admin/ai-evaluations/preflight?suite=lost-found"));
        Assert.Equal(0, preflight.GetProperty("apiCallsRequired").GetInt32());
        var usageBefore = preflight.GetProperty("usage").GetProperty("dailyUsed").GetInt32();
        var created = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/admin/ai-evaluations/runs", new { suite = "lost-found" }));
        var runId = created.GetProperty("id").GetGuid();
        Assert.Equal(0, created.GetProperty("apiCallsRequired").GetInt32());
        await CampusFixture.Csrf(client);
        await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/resume", new { }));

        string state = "running";
        for (var stepNumber = 0; stepNumber < 20 && state == "running"; stepNumber++)
        {
            await CampusFixture.Csrf(client);
            var step = await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/next", new { }));
            state = step.GetProperty("runState").GetString()!;
        }
        Assert.Equal("completed", state);

        var detail = await CampusFixture.Response(await client.GetAsync($"/api/v1/admin/ai-evaluations/runs/{runId}"));
        var cases = detail.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(20, cases.Length);
        Assert.All(cases, row => Assert.Equal("cached", row.GetProperty("state").GetString()));
        var absentImage = Assert.Single(cases, row => row.GetProperty("caseKey").GetString() == "query-08-absent-image");
        var actual = absentImage.GetProperty("actual");
        Assert.Equal("gemini-embedding-2-google-768-v1", actual.GetProperty("revision").GetString());
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("top1").ValueKind);
        Assert.Equal(JsonValueKind.Null, actual.GetProperty("top5").ValueKind);
        var candidates = actual.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(6, candidates.Length);
        var top = candidates[0];
        var topId = top.GetProperty("itemId").GetString();
        var topScore = top.GetProperty("similarity").GetDouble();
        output.WriteLine($"Corrected absent image fixture; application top candidate={topId}, raw similarity={topScore:F6}. No-match threshold remains unapproved.");
        Assert.True(topScore < 0.99, "The absent image fixture must not be identical to a gallery image.");

        using var usageScope = host.Services.CreateScope();
        var db = usageScope.ServiceProvider.GetRequiredService<CampusDb>();
        var today = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero);
        var used = await db.InferenceUsage.AsNoTracking().Where(row => row.Provider == "gemini" && row.Window == "day" && row.WindowStart == dayStart)
            .Select(row => row.Count).SingleAsync();
        Assert.Equal(usageBefore, used); // Replaying saved vectors must not consume another provider request.
    }
}
