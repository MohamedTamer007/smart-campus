using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

[ApiController, Authorize, Route("api/v1/admin/operations")]
public sealed class OperationsController(
    CampusDb db,
    Access access,
    IConfiguration config,
    IHttpClientFactory clients,
    TimeProvider clock) : ControllerBase
{
    private sealed record WorkerHealth(bool Ready, string Model, string Revision);

    // This endpoint reports configuration and process state only. It never calls an AI provider.
    private static AiReadinessResponse AiReadiness(IConfiguration settings) => new(
        settings["AI:Provider"] ?? "openrouter",
        AiProviders.Configured(settings),
        AiProviders.CanReviewImage(settings),
        new[] { "FaultTypes", "Priorities", "Risks" }.All(key =>
            (settings.GetSection("AI:" + key).Get<string[]>() ?? []).Length > 0),
        settings.GetValue<bool>("AI:AllowNonSensitiveInference"),
        settings.GetValue<bool>("AI:AllowNonSensitiveImages"),
        string.Equals(settings["AI:ProviderDataCollection"], "allow", StringComparison.OrdinalIgnoreCase));

    [HttpGet]
    public async Task<ActionResult<OperationsResponse>> Status(CancellationToken cancellation)
    {
        Access.Require((await access.Current()).Admin);
        var now = clock.GetUtcNow().ToUniversalTime();
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var jobs = await db.Jobs.GroupBy(job => new { job.Kind, job.State })
            .Select(group => new JobStateResponse(group.Key.Kind, group.Key.State, group.Count(), group.Max(job => job.Attempts)))
            .ToArrayAsync(cancellation);
        var used = await db.InferenceUsage.Where(usage => usage.Window == "day" && usage.WindowStart == day && usage.Provider == "openrouter")
            .Select(usage => usage.Count).SumAsync(cancellation);
        var quotas = new[]
        {
            new ProviderQuotaResponse("openrouter", used, config.GetValue("AI:OpenRouterDailyRequestLimit", 50))
        };

        var worker = config["Search:Provider"] == "hosted"
            ? RemoteEmbeddings.Configured(config) ? "hosted_configured" : "hosted_blocked"
            : "unavailable";
        if (config["Search:Provider"] != "hosted" &&
            Uri.TryCreate(config["Search:WorkerUrl"] ?? "http://127.0.0.1:8123/", UriKind.Absolute, out var endpoint) &&
            endpoint.IsLoopback && endpoint.Scheme == "http")
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
        return new OperationsResponse(
            now,
            config["Deployment:Commit"] ?? "local",
            process.WorkingSet64,
            jobs,
            quotas,
            worker,
            RetrievalService.CalibrationApproved(config),
            AiReadiness(config));
    }
}
