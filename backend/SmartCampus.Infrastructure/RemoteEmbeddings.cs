using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Server-only Gemini Embedding 2 integration for Lost &amp; Found retrieval.
/// The model, endpoint, and 768-dimensional output are pinned here. This class
/// never logs request bodies, image data, provider responses, or API keys.
/// </summary>
public sealed class RemoteEmbeddings(IHttpClientFactory clients, IConfiguration config, IInferenceBudget budget, IMemoryCache? cache = null)
{
    public const string Model = "gemini-embedding-2";
    public const int Dimension = 768;
    public const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent";
    private const int MaximumImageBytes = 1_900_000;
    private const int MaximumResponseBytes = 131_073;
    private readonly IMemoryCache embeddingCache = cache ?? new MemoryCache(new MemoryCacheOptions { SizeLimit = 2_000 });

    public static string Revision(IConfiguration _) => "gemini-embedding-2-google-768-v1";

    public static bool Configured(IConfiguration configuration) =>
        configuration["Search:Provider"] == "hosted" &&
        configuration.GetValue<bool>("Search:ExternalProcessingApproved") &&
        configuration.GetValue<bool>("Search:GeminiExternalProcessingApproved") &&
        string.Equals(configuration["Search:GeminiProviderDataCollection"], "allow", StringComparison.OrdinalIgnoreCase) &&
        configuration.GetValue<bool>("AI:GeminiFreeTierOnly") &&
        !string.IsNullOrWhiteSpace(configuration["AI:GeminiKey"]) &&
        configuration.GetValue("AI:GeminiDailyRequestLimit", 50) > 0 &&
        configuration.GetValue("AI:GeminiRequestsPerMinute", 10) > 0;

    public Task<EmbeddingResponse> Passage(string description, CancellationToken cancellation = default) =>
        EmbedText("title: none | text: " + Rules.Required(description, 4000), cancellation);

    public Task<EmbeddingResponse> Query(string description, CancellationToken cancellation = default) =>
        EmbedText("task: search result | query: " + Rules.Required(description, 4000), cancellation);

    public async Task<EmbeddingResponse> Image(byte[] image, CancellationToken cancellation = default)
    {
        if (!config.GetValue<bool>("Search:ExternalImagesApproved") ||
            !config.GetValue<bool>("Search:GeminiExternalImagesApproved"))
            throw new RuleException("search_external_images_not_approved", 503);
        var normalized = CampusImageNormalizer.ToJpeg(image, MaximumImageBytes);
        return await EmbedContent(
            new object[] { new { inline_data = new { mime_type = "image/jpeg", data = Convert.ToBase64String(normalized) } } },
            "image:" + Convert.ToHexString(SHA256.HashData(normalized)), cancellation);
    }

    private Task<EmbeddingResponse> EmbedText(string text, CancellationToken cancellation) =>
        EmbedContent(new object[] { new { text } }, "text:" + text, cancellation);

    private async Task<EmbeddingResponse> EmbedContent(object[] parts, string cacheMaterial, CancellationToken cancellation)
    {
        if (!Configured(config)) throw new RuleException("search_provider_not_configured", 503);
        var revision = Revision(config);
        var cacheKey = "gemini-embedding-v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revision + "\n" + cacheMaterial)));
        if (embeddingCache.TryGetValue<EmbeddingResponse>(cacheKey, out var cached) && cached is not null)
        {
            Validate(cached, config);
            return cached;
        }

        await budget.Reserve("gemini", cancellation);
        using var client = clients.CreateClient("retrieval");
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("x-goog-api-key", config["AI:GeminiKey"]);
        request.Content = JsonContent.Create(new {
            content = new { parts },
            outputDimensionality = Dimension
        });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new RuleException("ai_rate_limited", 503);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new RuleException("ai_key_rejected", 503);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new RuleException("ai_access_denied", 503);
            if (!response.IsSuccessStatusCode)
                throw new RuleException("search_provider_unavailable", 503);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
            var bytes = new byte[MaximumResponseBytes];
            var count = await stream.ReadAtLeastAsync(bytes.AsMemory(), bytes.Length, throwOnEndOfStream: false, cancellationToken: cancellation);
            if (count == bytes.Length) throw new RuleException("invalid_embedding", 503);
            using var json = JsonDocument.Parse(bytes.AsMemory(0, count));
            var values = json.RootElement.GetProperty("embedding").GetProperty("values");
            if (values.ValueKind != JsonValueKind.Array) throw new RuleException("invalid_embedding", 503);
            var vector = values.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            var result = new EmbeddingResponse(Model, revision, vector.Length, vector);
            Validate(result, config);
            embeddingCache.Set(cacheKey, result, new MemoryCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15), Size = 1
            });
            return result;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or IndexOutOfRangeException or FormatException)
        {
            throw new RuleException("invalid_embedding", 503);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new RuleException("search_provider_unavailable", 503);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new RuleException("search_provider_unavailable", 503);
        }
    }

    public static void Validate(EmbeddingResponse embedding, IConfiguration config)
    {
        if (embedding.Model != Model || embedding.Revision != Revision(config) || embedding.Dimension != Dimension ||
            embedding.Vector is null || embedding.Vector.Length != Dimension ||
            embedding.Vector.Any(value => !float.IsFinite(value)) ||
            Math.Abs(embedding.Vector.Sum(value => (double)value * value) - 1d) > .01d)
            throw new RuleException("invalid_embedding", 503);
    }
}
