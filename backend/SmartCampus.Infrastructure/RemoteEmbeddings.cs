using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Server-only hosted multimodal embeddings. No remote URL or model can be
/// supplied by an API caller. Never log image bytes, API tokens, or responses.
/// Nemotron 3.5 Embed 8B requires "passage" for indexing and "query"
/// for text queries. This free service is NOT authorized for student images by
/// default. Real external image processing requires separate privacy approval.
/// </summary>
public sealed class RemoteEmbeddings(IHttpClientFactory clients, IConfiguration config, IInferenceBudget budget)
{
    public const string Model = "nvidia/nemotron-3.5-embed-8b";
    public const int Dimension = 1024;
    private const string OpenRouterEndpoint = "https://openrouter.ai/api/v1/embeddings";
    public const string OpenRouterModel = Model + ":free";

    public static string Revision(IConfiguration configuration) => "nemotron35-8b-openrouter-1024-v1";

    public static bool Configured(IConfiguration configuration)
    {
        if (configuration["Search:Provider"] != "hosted" || !configuration.GetValue<bool>("Search:ExternalProcessingApproved"))
            return false;
        return !string.IsNullOrWhiteSpace(configuration["AI:OpenRouterKey"]) &&
            configuration.GetValue("AI:OpenRouterDailyRequestLimit", 50) > 0 &&
            configuration.GetValue("AI:OpenRouterRequestsPerMinute", 20) > 0;
    }

    public Task<EmbeddingResponse> Passage(string description, CancellationToken cancellation = default) =>
        Embed(Rules.Required(description, 4000), "text", "passage", cancellation);

    public Task<EmbeddingResponse> Query(string description, CancellationToken cancellation = default) =>
        Embed(Rules.Required(description, 4000), "text", "query", cancellation);

    public Task<EmbeddingResponse> Image(byte[] image, CancellationToken cancellation = default)
    {
        if (!config.GetValue<bool>("Search:ExternalImagesApproved"))
            throw new RuleException("search_external_images_not_approved", 503);
        if (image.Length is <= 0 or > 5242880) throw new RuleException("invalid_upload");
        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp) ||
            codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > 16_000_000)
            throw new RuleException("invalid_upload");
        using var bitmap = SKBitmap.Decode(data) ?? throw new RuleException("invalid_upload");
        // Keep the codec-decoded pixels, dropping original file metadata.
        // Reject oversized outgoing JPEGs instead of silently sending large files.
        using var imageObject = SKImage.FromBitmap(bitmap);
        using var encoded = imageObject.Encode(SKEncodedImageFormat.Jpeg, 80);
        if (encoded is null || encoded.Size > 2_000_000) throw new RuleException("invalid_upload");
        return Embed("data:image/jpeg;base64," + Convert.ToBase64String(encoded.ToArray()), "image", "passage", cancellation);
    }

    private async Task<EmbeddingResponse> Embed(string input, string modality, string inputType, CancellationToken cancellation)
    {
        if (!Configured(config)) throw new RuleException("search_provider_not_configured", 503);
        var key = config["AI:OpenRouterKey"]!;
        // OpenRouter's multimodal embeddings API accepts content arrays for images.
        // Do not send private Storage URLs: embed the resized pixels directly.
        var suppliedInput = modality == "image"
            ? (object)new object[] { new { content = new object[] {
                new { type = "image_url", image_url = new { url = input } }
            } } }
            : input;
        var payload = new Dictionary<string, object> {
            ["model"] = OpenRouterModel,
            ["input"] = suppliedInput,
            ["input_type"] = inputType,
            ["encoding_format"] = "float",
            ["dimensions"] = Dimension
        };
        payload["provider"] = new {
            allow_fallbacks = false,
            data_collection = config["Search:ProviderDataCollection"] ?? "deny",
            max_price = new { prompt = 0, completion = 0 }
        };

        await budget.Reserve("openrouter", cancellation);
        using var client = clients.CreateClient("retrieval");
        using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(payload);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new RuleException("ai_rate_limited", 503);
            if (!response.IsSuccessStatusCode)
                throw new RuleException("search_provider_unavailable", 503);

            using var stream = await response.Content.ReadAsStreamAsync(cancellation);
            var bytes = new byte[131073];
            var count = await stream.ReadAtLeastAsync(bytes.AsMemory(), bytes.Length, throwOnEndOfStream: false, cancellationToken: cancellation);
            if (count == bytes.Length) throw new RuleException("invalid_embedding", 503);
            using var json = JsonDocument.Parse(bytes.AsMemory(0, count));
            var root = json.RootElement;
            var actualModel = root.GetProperty("model").GetString();
            if (actualModel != OpenRouterModel)
                throw new RuleException("invalid_embedding", 503);
            var data = root.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() != 1 ||
                data[0].GetProperty("index").GetInt32() != 0)
                throw new RuleException("invalid_embedding", 503);
            var vector = data[0].GetProperty("embedding").EnumerateArray().Select(number => number.GetSingle()).ToArray();
            var result = new EmbeddingResponse(Model, Revision(config), vector.Length, vector);
            Validate(result, config);
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
            embedding.Vector.Any(v => !float.IsFinite(v)) ||
            Math.Abs(embedding.Vector.Sum(v => (double)v * v) - 1d) > .01d)
            throw new RuleException("invalid_embedding", 503);
    }
}
