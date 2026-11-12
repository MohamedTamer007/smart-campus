using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

[ApiController, Authorize, Route("api/v1/admin/operations")]
public sealed class OperationsController(CampusDb db, Access access, IConfiguration config, IHttpClientFactory clients, TimeProvider clock, IInferenceBudget inferenceBudget) : ControllerBase
{
    private sealed record WorkerHealth(bool Ready, string Model, string Revision);

    // Configuration diagnostics only: do not call providers or reveal secrets here.
    private static AiReadinessResponse AiReadiness(IConfiguration config) => new(
        config["AI:Provider"] ?? "openrouter",
        AiProviders.Configured(config),
        AiProviders.CanReviewImage(config),
        new[] { "FaultTypes", "Priorities", "Risks" }.All(key =>
            (config.GetSection("AI:" + key).Get<string[]>() ?? []).Length > 0),
        config.GetValue<bool>("AI:AllowNonSensitiveInference"),
        config.GetValue<bool>("AI:AllowNonSensitiveImages"),
        !string.IsNullOrWhiteSpace(config["AI:NvidiaKey"]),
        !string.IsNullOrWhiteSpace(config["AI:OpenRouterKey"]),
        config.GetValue("AI:NvidiaDailyRequestLimit", 0) > 0 &&
            config.GetValue("AI:NvidiaRequestsPerMinute", 5) > 0,
        config.GetValue("AI:OpenRouterDailyRequestLimit", 50) > 0 &&
            config.GetValue("AI:OpenRouterRequestsPerMinute", 20) > 0,
        config.GetValue<bool>("AI:SyntheticSmokeTestsEnabled"),
        config.GetValue<bool>("AI:AdminSyntheticDiagnosticEnabled"));

    // Strict admin-only, fixed-fictional-input diagnostic. It NEVER enables real
    // maintenance AI, forwards user content, or changes production AI gates.
    // At most three attempts per UTC day on the whole deployment. Existing
    // provider quota is charged; no provider text/keys are returned.
    [HttpPost("ai-smoke")]
    public async Task<ActionResult<AiSmokeResponse>> AiSmoke(CancellationToken cancellation)
    {
        Access.Require((await access.Current()).Admin);
        if (!config.GetValue<bool>("AI:AdminSyntheticDiagnosticEnabled"))
            throw new SmartCampus.Domain.RuleException("ai_smoke_disabled", 503);
        var key = config["AI:NvidiaKey"];
        var daily = config.GetValue("AI:NvidiaDailyRequestLimit", 0);
        var minute = config.GetValue("AI:NvidiaRequestsPerMinute", 5);
        if (string.IsNullOrWhiteSpace(key) || daily <= 0 || minute <= 0)
            throw new SmartCampus.Domain.RuleException("ai_not_configured", 503);

        var now = DateTimeOffset.UtcNow;
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var id = "admin-synthetic-smoke:" + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var reserved = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO smart_campus."InferenceUsage" ("Key", "Provider", "Window", "WindowStart", "Count")
            VALUES ({id}, {"admin-synthetic-smoke"}, {"day"}, {day}, 1)
            ON CONFLICT ("Key") DO UPDATE SET "Count" = "InferenceUsage"."Count" + 1
            WHERE "InferenceUsage"."Count" < 3
            """, cancellation);
        if (reserved != 1)
            throw new SmartCampus.Domain.RuleException("ai_smoke_limit", 429, day.AddDays(1));

        // This configuration exists in memory for this one request only.
        // It cannot turn on the application-wide AI:AllowNonSensitiveInference
        // switch or introduce any real institutional taxonomy.
        var testConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:Provider"] = "nvidia",
            ["AI:AllowNonSensitiveInference"] = "true",
            ["AI:NvidiaKey"] = key,
            ["AI:NvidiaDailyRequestLimit"] = daily.ToString(CultureInfo.InvariantCulture),
            ["AI:NvidiaRequestsPerMinute"] = minute.ToString(CultureInfo.InvariantCulture),
            ["AI:FaultTypes:0"] = "plumbing",
            ["AI:Priorities:0"] = "low",
            ["AI:Risks:0"] = "low"
        }).Build();
        const string fictionalReport = "TEST ONLY. Fictional maintenance scenario: a dripping pipe in an imaginary test room. No real student, staff member, building or incident.";
        var testProvider = new AiProviders(clients, testConfig, inferenceBudget);
        await testProvider.Assess(fictionalReport, cancellation);
        return new AiSmokeResponse("validated", "nvidia", "openai/gpt-oss-20b");
    }

    [HttpGet]
    public async Task<ActionResult<OperationsResponse>> Status(CancellationToken cancellation)
    {
        Access.Require((await access.Current()).Admin);
        var now = clock.GetUtcNow().ToUniversalTime();
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var jobs = await db.Jobs.GroupBy(j => new { j.Kind, j.State })
            .Select(g => new JobStateResponse(g.Key.Kind, g.Key.State, g.Count(), g.Max(j => j.Attempts))).ToArrayAsync(cancellation);
        var used = await db.InferenceUsage.Where(u => u.Window == "day" && u.WindowStart == day)
            .Select(u => new { u.Provider, u.Count }).ToDictionaryAsync(u => u.Provider, u => u.Count, cancellation);
        var quotas = new[] {
            new ProviderQuotaResponse("openrouter", used.GetValueOrDefault("openrouter"), config.GetValue("AI:OpenRouterDailyRequestLimit", 50)),
            new ProviderQuotaResponse("nvidia", used.GetValueOrDefault("nvidia"), config.GetValue("AI:NvidiaDailyRequestLimit", 0))
        };
        var worker = config["Search:Provider"] == "hosted"
            ? RemoteEmbeddings.Configured(config) ? "hosted_configured" : "hosted_blocked"
            : "unavailable";
        if (config["Search:Provider"] != "hosted" && Uri.TryCreate(config["Search:WorkerUrl"] ?? "http://127.0.0.1:8123/", UriKind.Absolute, out var endpoint) && endpoint.IsLoopback && endpoint.Scheme == "http")
        {
            try
            {
                using var client = clients.CreateClient("worker");
                var health = await client.GetFromJsonAsync<WorkerHealth>(new Uri(endpoint, "health"), cancellation);
                if (health is { Ready: true } && health.Model == RetrievalService.Model && health.Revision == RetrievalService.Revision)
                    worker = "available";
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException) { }
        }
        using var process = Process.GetCurrentProcess();
        return new OperationsResponse(now, config["Deployment:Commit"] ?? "local", process.WorkingSet64, jobs, quotas, worker,
            RetrievalService.ApprovedThreshold(config).HasValue, AiReadiness(config));
    }
}
