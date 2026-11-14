using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class InferenceBudgetTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class TestClock(DateTimeOffset instant) : TimeProvider
    {
        public DateTimeOffset Instant { get; set; } = instant;
        public override DateTimeOffset GetUtcNow() => Instant;
    }
    private static IConfiguration Config(int daily, int minute) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["AI:OpenRouterDailyRequestLimit"] = daily.ToString(), ["AI:OpenRouterRequestsPerMinute"] = minute.ToString()
    }).Build();

    [DatabaseFact]
    public async Task Concurrent_inference_reservations_never_exceed_daily_budget()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-11-11T12:00:00Z"));
        var requests = Enumerable.Range(0, 12).Select(async _ => {
            using var scope = campus.Services.CreateScope();
            var budget = new InferenceBudget(scope.ServiceProvider.GetRequiredService<CampusDb>(), Config(3, 20), clock);
            try { await budget.Reserve("openrouter", CancellationToken.None); return true; }
            catch (RuleException failure)
            {
                Assert.Equal("ai_quota_exhausted", failure.Code);
                Assert.Equal(DateTimeOffset.Parse("2026-11-12T00:00:00Z"), failure.RetryAt);
                return false;
            }
        });
        Assert.Equal(3, (await Task.WhenAll(requests)).Count(success => success));
        using var inspection = campus.Services.CreateScope();
        var rows = await inspection.ServiceProvider.GetRequiredService<CampusDb>().InferenceUsage.Where(u => u.WindowStart == clock.Instant).ToArrayAsync();
        Assert.Equal(3, Assert.Single(rows).Count); // minute window; the day starts at 00:00 UTC.
    }

    [DatabaseFact]
    public async Task Minute_limit_does_not_spend_daily_budget_and_resumes_next_window()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-11-12T12:34:00Z"));
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var budget = new InferenceBudget(db, Config(10, 1), clock);
        await budget.Reserve("openrouter", CancellationToken.None);
        var failure = await Assert.ThrowsAsync<RuleException>(() => budget.Reserve("openrouter", CancellationToken.None));
        Assert.Equal(clock.Instant.AddMinutes(1), failure.RetryAt);
        var dayStart = clock.Instant.Date;
        Assert.Equal(1, await db.InferenceUsage.Where(u => u.Provider == "openrouter" && u.Window == "day" && u.WindowStart == new DateTimeOffset(dayStart, TimeSpan.Zero)).Select(u => u.Count).SingleAsync());
        clock.Instant = clock.Instant.AddMinutes(1);
        await budget.Reserve("openrouter", CancellationToken.None);
        Assert.Equal(2, await db.InferenceUsage.Where(u => u.Provider == "openrouter" && u.Window == "day" && u.WindowStart == new DateTimeOffset(dayStart, TimeSpan.Zero)).Select(u => u.Count).SingleAsync());
    }

    [DatabaseFact]
    public async Task Non_openrouter_provider_is_rejected_without_counter_rows()
    {
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var budget = new InferenceBudget(db, Config(50, 20), TimeProvider.System);
        var failure = await Assert.ThrowsAsync<RuleException>(() => budget.Reserve("nvidia", CancellationToken.None));
        Assert.Equal("ai_not_configured", failure.Code);
        Assert.False(await db.InferenceUsage.AnyAsync(u => u.Provider == "nvidia"));
    }

    [DatabaseFact]
    public async Task Daily_budget_resets_only_at_UTC_boundary()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2026-11-13T23:59:59Z"));
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var budget = new InferenceBudget(db, Config(1, 20), clock);
        await budget.Reserve("openrouter", CancellationToken.None);
        var failure = await Assert.ThrowsAsync<RuleException>(() => budget.Reserve("openrouter", CancellationToken.None));
        Assert.Equal(DateTimeOffset.Parse("2026-11-14T00:00:00Z"), failure.RetryAt);
        clock.Instant = clock.Instant.AddSeconds(1);
        await budget.Reserve("openrouter", CancellationToken.None);
        Assert.Equal(2, await db.InferenceUsage.CountAsync(u => u.Provider == "openrouter" && u.Window == "day" &&
            u.WindowStart >= DateTimeOffset.Parse("2026-11-13T00:00:00Z") && u.WindowStart <= clock.Instant && u.Count == 1));
    }

    [DatabaseFact]
    public async Task Gemini_daily_and_minute_budgets_are_independent_from_OpenRouter()
    {
        var clock = new TestClock(DateTimeOffset.Parse("2040-11-13T12:34:00Z"));
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["AI:GeminiDailyRequestLimit"] = "2", ["AI:GeminiRequestsPerMinute"] = "1"
        }).Build();
        var budget = new InferenceBudget(db, config, clock);
        await budget.Reserve("gemini", CancellationToken.None);
        var failure = await Assert.ThrowsAsync<RuleException>(() => budget.Reserve("gemini", CancellationToken.None));
        Assert.Equal("ai_quota_exhausted", failure.Code);
        Assert.Equal(clock.Instant.AddMinutes(1), failure.RetryAt);
        var day = new DateTimeOffset(clock.Instant.UtcDateTime.Date, TimeSpan.Zero);
        Assert.Equal(1, await db.InferenceUsage.Where(row => row.Provider == "gemini" && row.Window == "day" && row.WindowStart == day).Select(row => row.Count).SingleAsync());
        Assert.False(await db.InferenceUsage.AnyAsync(row => row.Provider == "openrouter" && row.Window == "day" && row.WindowStart == day));
    }
}
