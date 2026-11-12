using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
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
    // At most five attempts per UTC day on the whole deployment. Existing
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

        await ReserveSyntheticAttempt(cancellation);

        // Basic NVIDIA chat-completions connectivity probe, not classification.
        // It never turns on real AI or sends a real report or user prompt.
        await inferenceBudget.Reserve("nvidia", cancellation);
        using var client = clients.CreateClient("inference");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        const string fictionalReport = "TEST ONLY. An imaginary dripping pipe in an imaginary room. No actual campus, student, staff member or incident.";
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("https://integrate.api.nvidia.com/v1/chat/completions",
                new
                {
                    model = "openai/gpt-oss-20b",
                    messages = new object[]
                    {
                        new { role = "system", content = "This is a connectivity diagnostic with fictional information only. Reply briefly and safely." },
                        new { role = "user", content = fictionalReport + " Reply with a short acknowledgement." }
                    },
                    temperature = 0.6,
                    max_tokens = 1024,
                    reasoning_effort = "low",
                    stream = false
                }, cancellation);
        }
        catch (HttpRequestException) { throw new SmartCampus.Domain.RuleException("ai_connection_failed", 503); }
        catch (TaskCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new SmartCampus.Domain.RuleException("ai_timeout", 503); }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new SmartCampus.Domain.RuleException("ai_key_rejected", 503);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new SmartCampus.Domain.RuleException("ai_access_denied", 503);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new SmartCampus.Domain.RuleException("ai_provider_rate_limited", 503);
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.BadRequest)
                throw new SmartCampus.Domain.RuleException("ai_request_rejected", 503);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new SmartCampus.Domain.RuleException("ai_provider_unavailable", 503);
            try
            {
                // Never return or log the model output.
                var responseBody = await response.Content.ReadAsStringAsync(cancellation);
                if (responseBody.Length > 100_000)
                    throw new SmartCampus.Domain.RuleException("ai_invalid_response", 503);
                using var json = JsonDocument.Parse(responseBody);
                var choice = json.RootElement.GetProperty("choices")[0];
                var content = choice.GetProperty("message").GetProperty("content");
                if (choice.GetProperty("finish_reason").GetString() != "stop" ||
                    content.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(content.GetString()))
                    throw new SmartCampus.Domain.RuleException("ai_invalid_response", 503);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            { throw new SmartCampus.Domain.RuleException("ai_invalid_response", 503); }
        }
        return new AiSmokeResponse("validated", "nvidia", "openai/gpt-oss-20b");
    }

    // Real model inference, but only against fixed fictional text.
    // Shares the same five-per-UTC-day cap as the connectivity diagnostic.
    // Never accepts text, media, report identifiers, or actual user data.
    [HttpPost("ai-demo")]
    public async Task<ActionResult<AiDemoResponse>> AiDemo(CancellationToken cancellation)
    {
        Access.Require((await access.Current()).Admin);
        if (!config.GetValue<bool>("AI:AdminSyntheticDiagnosticEnabled"))
            throw new SmartCampus.Domain.RuleException("ai_smoke_disabled", 503);
        var key = config["AI:NvidiaKey"];
        var daily = config.GetValue("AI:NvidiaDailyRequestLimit", 0);
        var minute = config.GetValue("AI:NvidiaRequestsPerMinute", 5);
        if (string.IsNullOrWhiteSpace(key) || daily <= 0 || minute <= 0)
            throw new SmartCampus.Domain.RuleException("ai_not_configured", 503);

        await ReserveSyntheticAttempt(cancellation);

        // Only this request receives these fictional labels; the real
        // AI:AllowNonSensitiveInference flag remains false globally.
        var fictionalSettings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:Provider"] = "nvidia",
            ["AI:AllowNonSensitiveInference"] = "true",
            ["AI:NvidiaKey"] = key,
            ["AI:NvidiaDailyRequestLimit"] = daily.ToString(CultureInfo.InvariantCulture),
            ["AI:NvidiaRequestsPerMinute"] = minute.ToString(CultureInfo.InvariantCulture),
            ["AI:FaultTypes:0"] = "plumbing",
            ["AI:FaultTypes:1"] = "electrical",
            ["AI:FaultTypes:2"] = "other",
            ["AI:Priorities:0"] = "low",
            ["AI:Priorities:1"] = "medium",
            ["AI:Priorities:2"] = "high",
            ["AI:Risks:0"] = "low",
            ["AI:Risks:1"] = "medium",
            ["AI:Risks:2"] = "high"
        }).Build();
        const string fictionalReport = "TEST ONLY. In an imaginary empty room, a fictional water pipe leaks near a sink. No real campus, building, person, or incident.";
        var provider = new AiProviders(clients, fictionalSettings, inferenceBudget);
        var assessment = await provider.Assess(fictionalReport, cancellation);
        return new AiDemoResponse("validated", "nvidia", "openai/gpt-oss-20b",
            assessment.Type, assessment.Priority, assessment.Risk, assessment.Explanation);
    }

    private async Task ReserveSyntheticAttempt(CancellationToken cancellation)
    {
        var now = clock.GetUtcNow().ToUniversalTime();
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var id = "admin-synthetic-smoke:" + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var reserved = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO smart_campus."InferenceUsage" ("Key", "Provider", "Window", "WindowStart", "Count")
            VALUES ({id}, {"admin-synthetic-smoke"}, {"day"}, {day}, 1)
            ON CONFLICT ("Key") DO UPDATE SET "Count" = "InferenceUsage"."Count" + 1
            WHERE "InferenceUsage"."Count" < 5
            """, cancellation);
        if (reserved != 1)
            throw new SmartCampus.Domain.RuleException("ai_smoke_limit", 429, day.AddDays(1));
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
