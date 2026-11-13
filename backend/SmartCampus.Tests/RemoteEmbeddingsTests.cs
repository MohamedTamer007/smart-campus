using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class RemoteEmbeddingsTests
{
    private sealed class Budget : IInferenceBudget
    {
        public int Count { get; private set; }
        public Task Reserve(string provider, CancellationToken cancellation)
        {
            Assert.Equal("gemini", provider);
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class Boundary(HttpStatusCode status, string body) : HttpMessageHandler, IHttpClientFactory
    {
        public int Requests { get; private set; }
        public JsonElement? Sent { get; private set; }
        public Uri? Destination { get; private set; }
        public string? ApiKeyHeader { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("retrieval", name);
            return new HttpClient(this, disposeHandler: false);
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Destination = request.RequestUri;
            ApiKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : null;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Sent = payload.RootElement.Clone();
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private sealed class TransportFailureBoundary : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("private test transport detail");
    }

    private static IConfiguration Config(bool allowImages = false, bool allowExternal = true,
        string? dataCollection = "allow", bool freeTierOnly = true, string? key = "synthetic-key") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Search:Provider"] = "hosted",
            ["Search:ExternalProcessingApproved"] = allowExternal.ToString(),
            ["Search:GeminiExternalProcessingApproved"] = allowExternal.ToString(),
            ["Search:ExternalImagesApproved"] = allowImages.ToString(),
            ["Search:GeminiExternalImagesApproved"] = allowImages.ToString(),
            ["Search:GeminiProviderDataCollection"] = dataCollection,
            ["AI:GeminiKey"] = key,
            ["AI:GeminiFreeTierOnly"] = freeTierOnly.ToString(),
            ["AI:GeminiDailyRequestLimit"] = "50",
            ["AI:GeminiRequestsPerMinute"] = "10"
        }).Build();

    private static string Envelope(int dimension = RemoteEmbeddings.Dimension, bool normalized = true)
    {
        var vector = new float[dimension];
        vector[0] = normalized ? 1 : 10;
        return JsonSerializer.Serialize(new { embedding = new { values = vector } });
    }

    [Fact]
    public async Task Text_query_uses_pinned_Gemini_model_768_dimensions_and_secret_header()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var budget = new Budget();
        var response = await new RemoteEmbeddings(boundary, Config(), budget).Query("زجاجة زرقاء مفقودة near library");

        Assert.Equal(1, budget.Count);
        Assert.Equal(1, boundary.Requests);
        Assert.Equal(RemoteEmbeddings.Endpoint, boundary.Destination!.ToString());
        Assert.Equal(string.Empty, boundary.Destination.Query);
        Assert.Equal("synthetic-key", boundary.ApiKeyHeader);
        Assert.Equal(RemoteEmbeddings.Model, response.Model);
        Assert.Equal(RemoteEmbeddings.Dimension, response.Dimension);
        var body = boundary.Sent!.Value;
        Assert.Equal(RemoteEmbeddings.Dimension, body.GetProperty("outputDimensionality").GetInt32());
        Assert.Equal("task: search result | query: زجاجة زرقاء مفقودة near library",
            body.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.False(body.TryGetProperty("taskType", out _));
    }

    [Fact]
    public async Task Text_indexing_uses_document_prompt_and_a_new_revision()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var result = await new RemoteEmbeddings(boundary, Config(), new Budget()).Passage("Synthetic red bag");
        Assert.Equal("title: none | text: Synthetic red bag",
            boundary.Sent!.Value.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Contains("gemini-embedding-2", result.Revision);
        Assert.NotEqual("nemotron35-8b-openrouter-1024-v1", result.Revision);
        Assert.NotEqual(RetrievalService.Revision, result.Revision);
    }

    [Fact]
    public async Task Repeated_text_query_is_cached_without_spending_another_provider_request()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var budget = new Budget();
        var service = new RemoteEmbeddings(boundary, Config(), budget);
        var first = await service.Query("unique cache fixture 2026-11-13");
        var second = await service.Query("unique cache fixture 2026-11-13");
        Assert.Equal(first.Vector, second.Vector);
        Assert.Equal(1, boundary.Requests);
        Assert.Equal(1, budget.Count);
    }

    [Fact]
    public async Task Image_submission_is_blocked_without_provider_specific_image_approval()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var budget = new Budget();
        var imageBytes = BluePng();
        var failure = await Assert.ThrowsAsync<RuleException>(() =>
            new RemoteEmbeddings(boundary, Config(allowImages: false), budget).Image(imageBytes));
        Assert.Equal("search_external_images_not_approved", failure.Code);
        Assert.Equal(0, boundary.Requests);
        Assert.Equal(0, budget.Count);
    }

    [Fact]
    public async Task Approved_image_is_sent_as_metadata_free_small_jpeg_inline_data()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var response = await new RemoteEmbeddings(boundary, Config(allowImages: true), new Budget()).Image(BluePng());
        Assert.Equal(RemoteEmbeddings.Dimension, response.Dimension);
        var body = boundary.Sent!.Value;
        Assert.Equal(RemoteEmbeddings.Dimension, body.GetProperty("outputDimensionality").GetInt32());
        var part = body.GetProperty("content").GetProperty("parts")[0].GetProperty("inline_data");
        Assert.Equal("image/jpeg", part.GetProperty("mime_type").GetString());
        var normalized = Convert.FromBase64String(part.GetProperty("data").GetString()!);
        Assert.InRange(normalized.Length, 1, CampusImageNormalizer.MaximumNormalizedBytes);
        using var normalizedData = SKData.CreateCopy(normalized);
        using var codec = SKCodec.Create(normalizedData);
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec!.EncodedFormat);
        Assert.InRange(codec.Info.Width, 1, CampusImageNormalizer.MaximumEdge);
        Assert.InRange(codec.Info.Height, 1, CampusImageNormalizer.MaximumEdge);
    }

    [Fact]
    public void Large_supported_image_is_resized_and_compressed_without_client_side_preprocessing()
    {
        using var bitmap = new SKBitmap(3000, 1700);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(245, 247, 250));
        using var paint = new SKPaint { IsAntialias = true, StrokeWidth = 5 };
        for (var index = 0; index < 1500; index++)
        {
            paint.Color = new SKColor((byte)(index * 29), (byte)(index * 47), (byte)(index * 71));
            canvas.DrawLine(index * 2, 0, 3000 - index, 1700, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var upload = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        Assert.True(upload!.Size < CampusImageNormalizer.MaximumUploadBytes);

        var normalized = CampusImageNormalizer.ToJpeg(upload.ToArray());

        Assert.InRange(normalized.Length, 1, CampusImageNormalizer.MaximumNormalizedBytes);
        using var data = SKData.CreateCopy(normalized);
        using var codec = SKCodec.Create(data);
        Assert.True(Math.Max(codec!.Info.Width, codec.Info.Height) <= CampusImageNormalizer.MaximumEdge);
    }

    [Theory]
    [InlineData(false, "allow", true, "search_provider_not_configured")]
    [InlineData(true, "deny", true, "search_provider_not_configured")]
    [InlineData(true, null, true, "search_provider_not_configured")]
    [InlineData(true, "allow", false, "search_provider_not_configured")]
    [InlineData(true, "allow", true, "search_provider_not_configured", null)]
    public async Task Missing_privacy_free_tier_or_key_gate_never_sends_request(bool allowExternal, string? dataCollection,
        bool freeTierOnly, string expected, string? key = "synthetic-key")
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var budget = new Budget();
        var config = Config(allowExternal: allowExternal, dataCollection: dataCollection, freeTierOnly: freeTierOnly, key: key);
        Assert.False(RemoteEmbeddings.Configured(config));
        var failure = await Assert.ThrowsAsync<RuleException>(() => new RemoteEmbeddings(boundary, config, budget).Query("Synthetic blue bottle"));
        Assert.Equal(expected, failure.Code);
        Assert.Equal(0, boundary.Requests);
        Assert.Equal(0, budget.Count);
    }

    [Theory]
    [InlineData(429, "{}", "ai_rate_limited")]
    [InlineData(403, "{}", "ai_access_denied")]
    [InlineData(503, "{}", "search_provider_unavailable")]
    [InlineData(200, "{}", "invalid_embedding")]
    public async Task Provider_errors_fail_closed_without_retry(int status, string body, string expected)
    {
        using var boundary = new Boundary((HttpStatusCode)status, body);
        var failure = await Assert.ThrowsAsync<RuleException>(() =>
            new RemoteEmbeddings(boundary, Config(), new Budget()).Query("Synthetic blue bottle"));
        Assert.Equal(expected, failure.Code);
        Assert.Equal(1, boundary.Requests);
    }

    [Fact]
    public async Task Provider_transport_errors_are_sanitized()
    {
        using var boundary = new TransportFailureBoundary();
        var failure = await Assert.ThrowsAsync<RuleException>(() =>
            new RemoteEmbeddings(boundary, Config(), new Budget()).Query("transport failure unique 2026"));
        Assert.Equal("search_provider_unavailable", failure.Code);
        Assert.DoesNotContain("private test transport detail", failure.ToString());
    }

    [Fact]
    public async Task Wrong_embedding_dimension_or_norm_is_rejected()
    {
        foreach (var body in new[] { Envelope(dimension: 4), Envelope(normalized: false) })
        {
            using var boundary = new Boundary(HttpStatusCode.OK, body);
            var failure = await Assert.ThrowsAsync<RuleException>(() =>
                new RemoteEmbeddings(boundary, Config(), new Budget()).Query("invalid vector unique " + body.Length));
            Assert.Equal("invalid_embedding", failure.Code);
        }
    }

    [Fact]
    public void Corrupt_image_is_rejected()
    {
        var failure = Assert.Throws<RuleException>(() => CampusImageNormalizer.ToJpeg([1, 2, 3, 4]));
        Assert.Equal("invalid_upload", failure.Code);
    }

    [Fact]
    public void Upload_over_the_shared_five_megabyte_limit_is_rejected()
    {
        var failure = Assert.Throws<RuleException>(() => CampusImageNormalizer.ToJpeg(new byte[CampusImageNormalizer.MaximumUploadBytes + 1]));
        Assert.Equal("invalid_upload", failure.Code);
    }

    private static byte[] BluePng()
    {
        using var bitmap = new SKBitmap(32, 24);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var bytes = image.Encode(SKEncodedImageFormat.Png, 90);
        return bytes!.ToArray();
    }
}
