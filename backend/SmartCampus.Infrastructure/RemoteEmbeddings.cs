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
/// NVIDIA's Nemotron 3.5 model uses "passage" for image/document indexing
/// and "query" only for text queries. Different providers are not mixed in one
/// vector index without independent calibration.
/// </summary>
public sealed class RemoteEmbeddings(IHttpClientFactory clients, IConfiguration config, IInferenceBudget budget)
{
    public const string Model = "nvidia/nemotron-3.5-embed-1b";
    public const int Dimension = 1024;
    private const string NvidiaEndpoint = "https://integrate.api.nvidia.com/v1/embeddings";
    private const string OpenRouterEndpoint = "https://openrouter.ai/api/v1/embeddings";
    private const string OpenRouterModel = Model + ":free";

    public static string Provider(IConfiguration configuration) => configuration["Search:Provider"] ?? "nvidia";
    public static string Revision(IConfiguration configuration) => "nemotron35-1b-1024-" + Provider(configuration) + "-v1";

    public static bool Configured(IConfiguration configuration)
    {
        var provider = Provider(configuration);
        if (provider is not ("nvidia" or "openrouter") || !configuration.GetValue<bool>("Search:ExternalProcessingApproved"))
            return false;
        return !string.IsNullOrWhiteSpace(configuration[provider == "nvidia" ? "AI:NvidiaKey" : "AI:OpenRouterKey"]) &&
            configuration.GetValue("AI:" + (provider == "nvidia" ? "NvidiaDailyRequestLimit" : "OpenRouterDailyRequestLimit"),
                provider == "nvidia" ? 0 : 50) > 0;
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
        var scale = Math.Min(1, 800d / Math.Max(bitmap.Width, bitmap.Height));
        using var resized = scale < 1 ? bitmap.Resize(new SKImageInfo(
            Math.Max(1, (int)(bitmap.Width * scale)), Math.Max(1, (int)(bitmap.Height * scale))), SKFilterQuality.Medium) : null;
        using var imageObject = SKImage.FromBitmap(resized ?? bitmap);
        using var encoded = imageObject.Encode(SKEncodedImageFormat.Jpeg, 80);
        if (encoded is null || encoded.Size > 2_000_000) throw new RuleException("invalid_upload");
        return Embed("data:image/jpeg;base64," + Convert.ToBase64String(encoded.ToArray()), "image", "passage", cancellation);
    }

    private async Task<EmbeddingResponse> Embed(string input, string modality, string inputType, CancellationToken cancellation)
    {
        if (!Configured(config)) throw new RuleException("search_provider_not_configured", 503);
        var provider = Provider(config);
        var nvidia = provider == "nvidia";
        var key = config[nvidia ? "AI:NvidiaKey" : "AI:OpenRouterKey"]!;
        // These vendor APIs use the same fixed model, but their transport schemas
        // differ for image content. Treat output revisions as provider-specific.
        var suppliedInput = nvidia
            ? (object)new[] { input }
            : modality == "image"
                ? new object[] { new { content = new object[] {
                    new { type = "image_url", image_url = new { url = input } }
                } } }
                : input;
        var payload = new Dictionary<string, object> {
            ["model"] = nvidia ? Model : OpenRouterModel,
            ["input"] = suppliedInput,
            ["input_type"] = inputType,
            ["encoding_format"] = "float",
            ["dimensions"] = Dimension
        };
        if (nvidia)
        {
            payload["modality"] = modality;
            payload["embedding_type"] = "float";
            payload["truncate"] = "END";
        }
        else payload["provider"] = new { allow_fallbacks = false, data_collection = "deny", max_price = new { prompt = 0, completion = 0 } };

        await budget.Reserve(provider, cancellation);
        using var client = clients.CreateClient("retrieval");
        using var request = new HttpRequestMessage(HttpMethod.Post, nvidia ? NvidiaEndpoint : OpenRouterEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new RuleException("ai_rate_limited", 503);
        if (!response.IsSuccessStatusCode)
            throw new RuleException("search_provider_unavailable", 503);

        using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        var bytes = new byte[131073];
        var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellation);
        if (count == bytes.Length) throw new RuleException("invalid_embedding", 503);
        try
        {
            using var json = JsonDocument.Parse(bytes.AsMemory(0, count));
            var root = json.RootElement;
            var expectedModel = nvidia ? Model : OpenRouterModel;
            if (root.GetProperty("model").GetString() != expectedModel)
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
