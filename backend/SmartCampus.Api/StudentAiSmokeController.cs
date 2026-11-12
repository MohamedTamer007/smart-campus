using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

/// <summary>
/// A fixed fictional provider connectivity test for verified, allowlisted
/// users in a controlled synthetic-data deployment only. Never forwards user
/// input, maintenance reports, photos or third-party private information.
/// </summary>
[ApiController, Authorize, Route("api/v1/ai/smoke")]
public sealed class StudentAiSmokeController(
    CampusDb db, Access access, IConfiguration config, TimeProvider clock, AiProviders providers) : ControllerBase
{
    private const string Example = "TEST ONLY. Fictional campus maintenance scenario: a dripping tap in an imaginary room. No real person, building or incident.";
    private const int TotalTestAttemptsPerUtcDay = 3;

    private bool Enabled() =>
        config["Release:Mode"] == "controlled" &&
        config.GetValue<bool>("AI:SyntheticSmokeTestsEnabled") &&
        config.GetValue<bool>("AI:SyntheticUserSmokeTestsEnabled") &&
        AiProviders.Configured(config);

    [HttpGet]
    public async Task<object> Status()
    {
        await access.Current(); // Require confirmed, currently authorized university account.
        return new { enabled = Enabled() };
    }

    [HttpPost]
    public async Task<ActionResult<AiSmokeResponse>> Test(CancellationToken cancellation)
    {
        var actor = await access.Current();
        if (!Enabled()) throw new RuleException("ai_smoke_disabled", 503);
        await ReserveSingleAttempt(actor.Id, cancellation);
        // The provider reserves its own day/minute quota as well.
        // A failed attempt is not automatically refunded: avoid repeated API spend.
        await providers.Assess(Example, cancellation);
        return new AiSmokeResponse("validated",
            AiProviders.SelectedProvider(config, hasApprovedImage: false),
            AiProviders.SelectedModel(config, hasApprovedImage: false));
    }

    private async Task ReserveSingleAttempt(Guid userId, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow().ToUniversalTime();
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var stamp = start.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var totalKey = "synthetic-smoke:day:" + stamp;
        var userKey = "synthetic-smoke:user:" + userId.ToString("N") + ":" + stamp;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);

        var total = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO smart_campus."InferenceUsage" ("Key", "Provider", "Window", "WindowStart", "Count")
            VALUES ({totalKey}, {"synthetic-smoke"}, {"day"}, {start}, 1)
            ON CONFLICT ("Key") DO UPDATE SET "Count" = "InferenceUsage"."Count" + 1
            WHERE "InferenceUsage"."Count" < {TotalTestAttemptsPerUtcDay}
            """, cancellation);
        if (total != 1) throw new RuleException("ai_smoke_limit", 429, start.AddDays(1));

        var user = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO smart_campus."InferenceUsage" ("Key", "Provider", "Window", "WindowStart", "Count")
            VALUES ({userKey}, {"synthetic-smoke"}, {"user-day"}, {start}, 1)
            ON CONFLICT ("Key") DO NOTHING
            """, cancellation);
        if (user != 1) throw new RuleException("ai_smoke_limit", 429, start.AddDays(1));
        await transaction.CommitAsync(cancellation);
    }
}
