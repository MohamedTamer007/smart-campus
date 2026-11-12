using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

[ApiController, Authorize, Route("api/v1/admin/operations")]
public sealed class OperationsController(CampusDb db, Access access, IConfiguration config, IHttpClientFactory clients, TimeProvider clock, AiProviders aiProviders) : ControllerBase
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
        config.GetValue<bool>("AI:SyntheticSmokeTestsEnabled"));

    // Only an administrator explicitly requesting this endpoint can spend one
    // app-budgeted provider attempt. No request body, photos, user records,
    // staff reports, keys or model output are accepted or returned.
    // Off by default; enable only in an isolated, synthetic-data test deployment.
    [HttpPost("ai-smoke")]
    public async Task<ActionResult<AiSmokeResponse>> AiSmoke(CancellationToken cancellation)
    {
        Access.Require((await access.Current()).Admin);
        if (!config.GetValue<bool>("AI:SyntheticSmokeTestsEnabled"))
            throw new SmartCampus.Domain.RuleException("ai_smoke_disabled", 503);
        if (!AiProviders.Configured(config))
            throw new SmartCampus.Domain.RuleException("ai_not_configured", 503);
        const string fictionalReport = "TEST ONLY. Fictional maintenance scenario: a dripping pipe in an imaginary test room. No real student, staff member, building or incident.";
        await aiProviders.Assess(fictionalReport, cancellation);
        return new AiSmokeResponse("validated", AiProviders.SelectedProvider(config, hasApprovedImage: false),
            AiProviders.SelectedModel(config, hasApprovedImage: false));
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
