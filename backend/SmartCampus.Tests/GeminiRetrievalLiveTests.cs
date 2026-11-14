using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Npgsql;
using SmartCampus.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SmartCampus.Tests;

public sealed class GeminiRetrievalLiveFactAttribute : FactAttribute
{
    public GeminiRetrievalLiveFactAttribute()
    {
        var connection = Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB");
        var key = Environment.GetEnvironmentVariable("SMART_CAMPUS_GEMINI_TEST_KEY");
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(key) ||
            !string.Equals(Environment.GetEnvironmentVariable("SMART_CAMPUS_RUN_LIVE_AI_TESTS"), "true", StringComparison.Ordinal))
        {
            Skip = "Requires explicit live-test opt-in, a separate Gemini test key, and an isolated SMART_CAMPUS_TEST_DB; otherwise no API request is made.";
            return;
        }

        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Database?.EndsWith("_test", StringComparison.Ordinal) != true || parsed.Host is not ("localhost" or "127.0.0.1"))
            Skip = "Live retrieval tests require loopback PostgreSQL with a database name ending in _test.";
    }
}

/// <summary>
/// Explicit, bounded live retrieval evaluation. It sends only the suite's fixed
/// fictional text and generated illustrations to Google's pinned Gemini API.
/// No key, provider response headers, or raw vectors are written to test output.
/// </summary>
public sealed class GeminiRetrievalLiveTests(CampusFixture campus, ITestOutputHelper output) : IClassFixture<CampusFixture>
{
    private const int MaximumOutboundRequests = 20;

    private sealed class GeminiOnlyClientFactory(string key) : IHttpClientFactory
    {
        private int requests;
        public int Requests => Volatile.Read(ref requests);

        public HttpClient CreateClient(string name)
        {
            if (name != "retrieval") throw new InvalidOperationException("Unexpected outbound client in the Gemini retrieval test.");
            var guard = new GeminiOnlyHandler(key, () => Interlocked.Increment(ref requests))
            {
                InnerHandler = new HttpClientHandler()
            };
            return new HttpClient(guard, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(45) };
        }
    }

    private sealed class GeminiOnlyHandler(string key, Func<int> reserve) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            if (request.Method != HttpMethod.Post || request.RequestUri?.ToString() != RemoteEmbeddings.Endpoint ||
                !request.Headers.TryGetValues("x-goog-api-key", out var values) || values.SingleOrDefault() != key)
                throw new InvalidOperationException("The live test blocked a request outside the pinned Gemini endpoint or test key.");
            if (reserve() > MaximumOutboundRequests)
                throw new InvalidOperationException("The bounded live test blocked an outbound request above its 20-request cap.");
            return base.SendAsync(request, cancellation);
        }
    }

    [GeminiRetrievalLiveFact]
    public async Task Saved_lost_found_gallery_runs_through_the_real_application_with_at_most_20_requests()
    {
        var key = Environment.GetEnvironmentVariable("SMART_CAMPUS_GEMINI_TEST_KEY")!;
        var connection = Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB")!;
        var clients = new GeminiOnlyClientFactory(key);
        using var host = campus.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Campus"] = connection,
                ["AI:GeminiKey"] = key,
                ["AI:GeminiFreeTierOnly"] = "true",
                ["AI:GeminiDailyRequestLimit"] = "50",
                ["AI:GeminiRequestsPerMinute"] = "10",
                ["Email:LocalSink"] = "true",
                ["RateLimits:AuthPerMinute"] = "1000",
                ["RateLimits:PerMinute"] = "10000"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CampusDb>>();
                services.AddDbContext<CampusDb>(options => options.UseNpgsql(connection,
                    postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "smart_campus")));
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(clients);
            });
        });

        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CampusFixture.Csrf(client);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(campus.Admin), password = campus.Password });
        Assert.True(login.IsSuccessStatusCode, "The isolated test administrator could not sign in.");
        await CampusFixture.Csrf(client);

        var preflight = await CampusFixture.Response(await client.GetAsync("/api/v1/admin/ai-evaluations/preflight?suite=lost-found"));
        Assert.Equal(20, preflight.GetProperty("caseCount").GetInt32());
        Assert.Equal(20, preflight.GetProperty("apiCallsRequired").GetInt32());
        var usage = preflight.GetProperty("usage");
        Assert.True(usage.GetProperty("dailyRemaining").GetInt32() >= 20, "The local request budget cannot cover the bounded suite.");
        Assert.Equal(50, usage.GetProperty("dailyLimit").GetInt32());
        Assert.Equal(0, usage.GetProperty("maximumPrice").GetDecimal());
        Assert.False(usage.GetProperty("paidFallbacks").GetBoolean());

        var created = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/admin/ai-evaluations/runs", new { suite = "lost-found" }));
        var runId = created.GetProperty("id").GetGuid();
        Assert.Equal(20, created.GetProperty("apiCallsRequired").GetInt32());
        await CampusFixture.Csrf(client);
        await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/resume", new { }));

        var requestStart = clients.Requests;
        var quotaWaits = 0;
        var state = "running";
        for (var stepNumber = 0; stepNumber < 24 && state == "running"; stepNumber++)
        {
            await CampusFixture.Csrf(client);
            var step = await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/next", new { }));
            state = step.GetProperty("runState").GetString()!;
            var pauseReason = step.TryGetProperty("pauseReason", out var pause) ? pause.GetString() : null;
            if (pauseReason == "quota")
            {
                quotaWaits++;
                Assert.InRange(quotaWaits, 1, 2);
                var nextMinute = step.GetProperty("usage").GetProperty("nextMinuteAt").GetDateTimeOffset();
                var delay = nextMinute - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(1);
                Assert.InRange(delay, TimeSpan.Zero, TimeSpan.FromMinutes(1));
                await Task.Delay(delay);
                await CampusFixture.Csrf(client);
                await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/resume", new { }));
                state = "running";
            }
            else if (step.GetProperty("case").ValueKind != JsonValueKind.Null)
            {
                var row = step.GetProperty("case");
                var actual = row.GetProperty("actual");
                output.WriteLine($"{row.GetProperty("caseKey").GetString()}: kind={row.GetProperty("kind").GetString()}, state={row.GetProperty("state").GetString()}, expected={row.GetProperty("expected").GetRawText()}, actual={actual.GetRawText()}, elapsedMs={row.GetProperty("elapsedMilliseconds").GetRawText()}, error={row.GetProperty("errorCode").GetRawText()}.");
            }
        }

        var detail = await CampusFixture.Response(await client.GetAsync($"/api/v1/admin/ai-evaluations/runs/{runId}"));
        var cases = detail.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal("completed", detail.GetProperty("run").GetProperty("state").GetString());
        Assert.Equal(20, cases.Length);
        Assert.All(cases, row => Assert.Equal("completed", row.GetProperty("state").GetString()));
        Assert.Equal(20, clients.Requests - requestStart);

        var positives = cases.Where(row => row.GetProperty("kind").GetString() is "embedding-query-text" or "embedding-query-image" &&
            row.GetProperty("expected").GetProperty("itemId").ValueKind == JsonValueKind.String).ToArray();
        var evaluatedTop1 = positives.Count(row => row.GetProperty("actual").GetProperty("top1").ValueKind is JsonValueKind.True or JsonValueKind.False);
        var top1 = positives.Count(row => row.GetProperty("actual").GetProperty("top1").ValueKind == JsonValueKind.True);
        var top5 = positives.Count(row => row.GetProperty("actual").GetProperty("top5").ValueKind == JsonValueKind.True);
        output.WriteLine($"Live Gemini lost/found evaluation: exact outbound requests={clients.Requests - requestStart}, positive queries={evaluatedTop1}, top1={top1}/{evaluatedTop1}, top5={top5}/{evaluatedTop1}, quota waits={quotaWaits}. No-match queries have no approved threshold and are reported as ranking observations only.");
        Assert.Equal(6, evaluatedTop1);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var today = DateTimeOffset.UtcNow;
        var day = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero);
        var usageCount = await db.InferenceUsage.AsNoTracking().Where(row => row.Provider == "gemini" && row.Window == "day" && row.WindowStart == day)
            .Select(row => row.Count).SingleAsync();
        Assert.Equal(20, usageCount);
    }
}
