using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public interface IInferenceBudget
{
    Task Reserve(string provider, CancellationToken cancellation);
}

/// <summary>Atomically reserves UTC day/minute attempt budgets before an external inference request.</summary>
public sealed class InferenceBudget(CampusDb db, IConfiguration config, TimeProvider clock) : IInferenceBudget
{
    public async Task Reserve(string provider, CancellationToken cancellation)
    {
        if (provider is not ("openrouter" or "nvidia")) throw new RuleException("ai_not_configured", 503);
        var daily = config.GetValue("AI:" + (provider == "nvidia" ? "NvidiaDailyRequestLimit" : "OpenRouterDailyRequestLimit"), provider == "nvidia" ? 0 : 50);
        var perMinute = config.GetValue("AI:" + (provider == "nvidia" ? "NvidiaRequestsPerMinute" : "OpenRouterRequestsPerMinute"), provider == "nvidia" ? 5 : 20);
        if (daily <= 0 || perMinute <= 0) throw new RuleException("ai_quota_not_configured", 503);
        var now = clock.GetUtcNow().ToUniversalTime();
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var minute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        await ReserveWindow(provider, "day", day, daily, day.AddDays(1), cancellation);
        await ReserveWindow(provider, "minute", minute, perMinute, minute.AddMinutes(1), cancellation);
        await transaction.CommitAsync(cancellation);
    }

    private async Task ReserveWindow(string provider, string window, DateTimeOffset start, int limit, DateTimeOffset reset, CancellationToken cancellation)
    {
        var key = provider + ":" + window + ":" + start.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO smart_campus."InferenceUsage" ("Key", "Provider", "Window", "WindowStart", "Count")
            VALUES ({key}, {provider}, {window}, {start}, 1)
            ON CONFLICT ("Key") DO UPDATE SET "Count" = "InferenceUsage"."Count" + 1
            WHERE "InferenceUsage"."Count" < {limit}
            """, cancellation);
        if (changed != 1) throw new RuleException("ai_quota_exhausted", 503, reset);
    }
}
