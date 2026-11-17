using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>One model call: instructions, user text, an optional already-approved image, and a size cap.</summary>
public sealed record LlmRequest(string System, string UserText, byte[]? Image, int MaxTokens);

/// <summary>The model's JSON text plus which provider and model actually answered.</summary>
public sealed record LlmReply(string Text, string Backend, string Model);

/// <summary>
/// Remembers, per provider, when it may be tried again after a quota or outage so a failing
/// provider is not retried on every request. In-memory and per process by design.
/// </summary>
public sealed class LlmHealth
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset Until, string Code)> cooling = new();

    public bool Cooling(string backend, DateTimeOffset now, out string code)
    {
        code = "";
        if (!cooling.TryGetValue(backend, out var entry)) return false;
        if (entry.Until <= now) { cooling.TryRemove(backend, out _); return false; }
        code = entry.Code;
        return true;
    }

    public void Trip(string backend, string code, DateTimeOffset until) => cooling[backend] = (until, code);
    public void Clear(string backend) => cooling.TryRemove(backend, out _);
}

/// <summary>
/// Runs a model call against an ordered list of providers and keys and returns the first
/// answer. A provider that is out of quota, rejecting the key or down is skipped and the
/// next key or provider is tried; the caller only sees an error when every one has failed.
/// Cross-provider use of Gemini needs the owner's flag AI:AllowCrossProviderFallback.
/// </summary>
public sealed class LlmChain(IHttpClientFactory clients, IConfiguration config, IInferenceBudget budget, LlmHealth health, TimeProvider? clock = null)
{
    public const string OpenRouterModel = "nvidia/nemotron-3.5-super-vl:free";
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    // Codes that mean "this provider or key cannot answer right now", so another may.
    private static readonly HashSet<string> Failover = new(StringComparer.Ordinal)
    {
        "ai_quota_exhausted", "ai_rate_limited", "ai_key_rejected", "ai_access_denied", "ai_unavailable",
        "ai_connection_failed", "ai_timeout", "invalid_ai_response", "ai_request_rejected"
    };
    // Codes tied to one key: the backup key of the same provider is worth trying.
    private static readonly HashSet<string> KeySpecific = new(StringComparer.Ordinal)
    { "ai_rate_limited", "ai_key_rejected", "ai_access_denied" };

    public static string[] Keys(IConfiguration c, string provider) =>
        new[] { c[$"AI:{provider}Key"], c[$"AI:{provider}KeyBackup"] }.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k!).ToArray();

    public static bool OpenRouterReady(IConfiguration c) =>
        (c["AI:Provider"] ?? "openrouter") == "openrouter" &&
        string.Equals(c["AI:ProviderDataCollection"], "allow", StringComparison.OrdinalIgnoreCase) &&
        Keys(c, "OpenRouter").Length > 0;

    public static bool GeminiReady(IConfiguration c) =>
        c.GetValue<bool>("AI:AllowCrossProviderFallback") && c.GetValue<bool>("AI:GeminiFreeTierOnly") &&
        Keys(c, "Gemini").Length > 0 && c.GetValue("AI:GeminiLlmDailyRequestLimit", 200) > 0 && c.GetValue("AI:GeminiLlmRequestsPerMinute", 10) > 0;

    public static string GeminiModel(IConfiguration c) => c["AI:GeminiLlmModel"] is { Length: > 0 } m ? m : "gemini-2.5-flash";

    public static bool Ready(IConfiguration c) => OpenRouterReady(c) || GeminiReady(c);

    /// <summary>Provider order: the configured primary first, then the other.</summary>
    private string[] Order()
    {
        var configured = (config["AI:FallbackOrder"] ?? "openrouter,gemini").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant()).Where(x => x is "openrouter" or "gemini").Distinct().ToList();
        foreach (var provider in new[] { "openrouter", "gemini" }) if (!configured.Contains(provider)) configured.Add(provider);
        return configured.ToArray();
    }

    public async Task<LlmReply> Complete(LlmRequest request, string purpose, CancellationToken cancellation)
    {
        RuleException? last = null;
        foreach (var backend in Order())
        {
            if (backend == "openrouter" ? !OpenRouterReady(config) : !GeminiReady(config)) continue;
            if (health.Cooling(backend, time.GetUtcNow(), out var coolingCode))
            {
                last ??= new RuleException(coolingCode, 503);
                continue;
            }
            foreach (var key in Keys(config, backend == "openrouter" ? "OpenRouter" : "Gemini"))
            {
                try
                {
                    var reply = backend == "openrouter"
                        ? await OpenRouter(request, purpose, key, cancellation)
                        : await Gemini(request, key, cancellation);
                    health.Clear(backend);
                    return reply;
                }
                catch (RuleException failure) when (Failover.Contains(failure.Code))
                {
                    last = failure;
                    // A rejected or rate-limited key may be fixed by the backup key; anything else moves to the next provider.
                    if (KeySpecific.Contains(failure.Code)) continue;
                    break;
                }
            }
            if (last is not null)
                health.Trip(backend, last.Code, last.RetryAt ?? time.GetUtcNow().AddSeconds(last.Code == "ai_key_rejected" ? 600 : 60));
        }
        throw last ?? new RuleException("ai_not_configured", 503);
    }

    private async Task<LlmReply> OpenRouter(LlmRequest request, string purpose, string key, CancellationToken cancellation)
    {
        using var client = clients.CreateClient("inference");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        object user = request.Image is null ? request.UserText
            : new object[] { new { type = "text", text = request.UserText }, new { type = "image_url", image_url = new { url = ImageUrl(request.Image) } } };
        var payload = new Dictionary<string, object>
        {
            ["model"] = OpenRouterModel, ["max_tokens"] = request.MaxTokens, ["temperature"] = 0,
            ["messages"] = new object[] { new { role = "system", content = request.System }, new { role = "user", content = user } },
            ["response_format"] = new { type = "json_object" },
            ["reasoning"] = new { enabled = false },
            ["provider"] = new { allow_fallbacks = false, data_collection = config["AI:ProviderDataCollection"] ?? "deny", max_price = new { prompt = 0, completion = 0 } }
        };
        var budgetName = purpose == "describe" ? "openrouter-describe" : "openrouter";
        var json = await Send(client, "https://openrouter.ai/api/v1/chat/completions", payload, budgetName, cancellation, envelope =>
        {
            using var response = JsonDocument.Parse(envelope);
            if (!string.Equals(response.RootElement.GetProperty("model").GetString(), OpenRouterModel, StringComparison.Ordinal))
                throw new RuleException("invalid_ai_response", 503);
        });
        return new LlmReply(AiProviders.ModelText(json), "openrouter", OpenRouterModel);
    }

    private async Task<LlmReply> Gemini(LlmRequest request, string key, CancellationToken cancellation)
    {
        using var client = clients.CreateClient("inference");
        client.DefaultRequestHeaders.Add("x-goog-api-key", key);
        var model = GeminiModel(config);
        var parts = new List<object> { new { text = request.UserText } };
        if (request.Image is not null) parts.Add(new { inline_data = new { mime_type = "image/jpeg", data = Convert.ToBase64String(CampusImageNormalizer.ToJpeg(request.Image)) } });
        var payload = new Dictionary<string, object>
        {
            ["systemInstruction"] = new { parts = new object[] { new { text = request.System } } },
            ["contents"] = new object[] { new { role = "user", parts } },
            ["generationConfig"] = new { maxOutputTokens = request.MaxTokens, temperature = 0, responseMimeType = "application/json" }
        };
        var json = await Send(client, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent",
            payload, "gemini-llm", cancellation, _ => { });
        try
        {
            using var document = JsonDocument.Parse(json);
            var candidate = document.RootElement.GetProperty("candidates")[0];
            if (candidate.GetProperty("finishReason").GetString() != "STOP") throw new RuleException("invalid_ai_response", 503);
            var text = candidate.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim() ?? "";
            return new LlmReply(AiProviders.StripFences(text), "gemini", model);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or IndexOutOfRangeException)
        {
            throw new RuleException("invalid_ai_response", 503);
        }
    }

    private static string ImageUrl(byte[] image) => "data:image/jpeg;base64," + Convert.ToBase64String(CampusImageNormalizer.ToJpeg(image));

    // Reserves quota, maps provider failures to stable codes without leaking bodies or keys.
    private async Task<string> Send(HttpClient client, string url, Dictionary<string, object> payload, string budgetProvider,
        CancellationToken cancellation, Action<string> validate)
    {
        await budget.Reserve(budgetProvider, cancellation);
        try
        {
            using var response = await client.PostAsJsonAsync(url, payload, cancellation);
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new RuleException("ai_key_rejected", 503);
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new RuleException("ai_access_denied", 503);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity) throw new RuleException("ai_request_rejected", 503);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var now = DateTimeOffset.UtcNow;
                var retry = response.Headers.RetryAfter?.Date ?? now.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
                throw new RuleException("ai_rate_limited", 503, now.AddSeconds(Math.Clamp((retry - now).TotalSeconds, 60, 86400)));
            }
            if (!response.IsSuccessStatusCode) throw new RuleException("ai_unavailable", 503);
            var json = await response.Content.ReadAsStringAsync(cancellation);
            if (json.Length > 100000) throw new RuleException("invalid_ai_response", 503);
            try { validate(json); }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
            { throw new RuleException("invalid_ai_response", 503); }
            return json;
        }
        catch (HttpRequestException) { throw new RuleException("ai_connection_failed", 503); }
        catch (System.IO.IOException) { throw new RuleException("ai_connection_failed", 503); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw new RuleException("ai_timeout", 503); }
    }
}
