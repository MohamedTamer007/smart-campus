using System.Net;
using System.Net.Http.Json;
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
            Assert.Equal("openrouter", provider);
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class Boundary(HttpStatusCode status, string body) : HttpMessageHandler, IHttpClientFactory
    {
        public int Requests { get; private set; }
        public JsonElement? Sent { get; private set; }
        public Uri? Destination { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("retrieval", name);
            return new HttpClient(this, disposeHandler: false);
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Destination = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Sent = payload.RootElement.Clone();
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static IConfiguration Config(bool allowImages = false, bool allowExternal = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Search:Provider"] = "hosted",
            ["Search:ExternalProcessingApproved"] = allowExternal.ToString(),
            ["Search:ExternalImagesApproved"] = allowImages.ToString(),
            ["AI:OpenRouterKey"] = "test-only-key-never-real",
            ["AI:OpenRouterDailyRequestLimit"] = "50",
            ["AI:OpenRouterRequestsPerMinute"] = "20"
        }).Build();

    private static string Envelope(string model = RemoteEmbeddings.OpenRouterModel, int dimension = RemoteEmbeddings.Dimension, bool normalized = true)
    {
        var vector = new float[dimension];
        vector[0] = normalized ? 1 : 10;
        return JsonSerializer.Serialize(new {
            model,
            data = new[] { new { index = 0, embedding = vector } }
        });
    }

    [Fact]
    public async Task Text_query_uses_selected_free_model_no_vendor_fallback_and_validated_vector()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var budget = new Budget();
        var response = await new RemoteEmbeddings(boundary, Config(), budget).Query("Synthetic blue bottle");
        Assert.Equal(1, budget.Count);
        Assert.Equal(1, boundary.Requests);
        Assert.Equal("https://openrouter.ai/api/v1/embeddings", boundary.Destination!.ToString());
        Assert.Equal("Bearer", boundary.AuthorizationScheme);
        Assert.Equal(RemoteEmbeddings.Model, response.Model);
        Assert.Equal(RemoteEmbeddings.Dimension, response.Dimension);
        var body = boundary.Sent!.Value;
        Assert.Equal(RemoteEmbeddings.OpenRouterModel, body.GetProperty("model").GetString());
        Assert.Equal("query", body.GetProperty("input_type").GetString());
        Assert.Equal("Synthetic blue bottle", body.GetProperty("input").GetString());
        Assert.Equal(RemoteEmbeddings.Dimension, body.GetProperty("dimensions").GetInt32());
        Assert.False(body.GetProperty("provider").GetProperty("allow_fallbacks").GetBoolean());
        Assert.Equal("deny", body.GetProperty("provider").GetProperty("data_collection").GetString());
        Assert.Equal(0, body.GetProperty("provider").GetProperty("max_price").GetProperty("prompt").GetInt32());
    }

    [Fact]
    public async Task Text_indexing_uses_passage_mode_and_separate_revision()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope(RemoteEmbeddings.Model));
        var result = await new RemoteEmbeddings(boundary, Config(), new Budget()).Passage("Synthetic red bag");
        Assert.Equal("passage", boundary.Sent!.Value.GetProperty("input_type").GetString());
        Assert.Contains("8b", result.Revision);
        Assert.NotEqual(RetrievalService.Revision, result.Revision);
    }

    [Fact]
    public async Task Image_submission_is_blocked_without_separate_approval()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var budget = new Budget();
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Blue);
        using var img = SKImage.FromBitmap(bitmap);
        using var bytes = img.Encode(SKEncodedImageFormat.Png, 90);
        var failure = await Assert.ThrowsAsync<RuleException>(() =>
            new RemoteEmbeddings(boundary, Config(), budget).Image(bytes.ToArray()));
        Assert.Equal("search_external_images_not_approved", failure.Code);
        Assert.Equal(0, boundary.Requests);
        Assert.Equal(0, budget.Count);
    }

    [Fact]
    public async Task Explicitly_approved_synthetic_image_uses_multimodal_content_and_passage_mode()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Blue);
        using var img = SKImage.FromBitmap(bitmap);
        using var bytes = img.Encode(SKEncodedImageFormat.Png, 90);
        var response = await new RemoteEmbeddings(boundary, Config(allowImages: true), new Budget()).Image(bytes.ToArray());
        Assert.Equal(RemoteEmbeddings.Dimension, response.Dimension);
        var body = boundary.Sent!.Value;
        Assert.Equal("passage", body.GetProperty("input_type").GetString());
        var image = body.GetProperty("input")[0].GetProperty("content")[0];
        Assert.Equal("image_url", image.GetProperty("type").GetString());
        Assert.StartsWith("data:image/jpeg;base64,", image.GetProperty("image_url").GetProperty("url").GetString());
    }

    [Fact]
    public async Task Unapproved_external_processing_never_sends_a_request()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, Envelope());
        var budget = new Budget();
        var failure = await Assert.ThrowsAsync<RuleException>(() =>
            new RemoteEmbeddings(boundary, Config(allowExternal: false), budget).Query("Synthetic blue bottle"));
        Assert.Equal("search_provider_not_configured", failure.Code);
        Assert.Equal(0, boundary.Requests);
        Assert.Equal(0, budget.Count);
    }

    [Theory]
    [InlineData(429, "{}", "ai_rate_limited")]
    [InlineData(503, "{}", "search_provider_unavailable")]
    [InlineData(200, "{}", "invalid_embedding")]
    public async Task Provider_errors_fail_closed(int status, string body, string expected)
    {
        using var boundary = new Boundary((HttpStatusCode)status, body);
        var failure = await Assert.ThrowsAsync<RuleException>(() =>
            new RemoteEmbeddings(boundary, Config(), new Budget()).Query("Synthetic blue bottle"));
        Assert.Equal(expected, failure.Code);
    }

    [Fact]
    public async Task Wrong_embedding_dimension_or_norm_is_rejected()
    {
        foreach (var body in new[] { Envelope(dimension: 4), Envelope(normalized: false), Envelope(model: "unexpected/paid") })
        {
            using var boundary = new Boundary(HttpStatusCode.OK, body);
            var failure = await Assert.ThrowsAsync<RuleException>(() =>
                new RemoteEmbeddings(boundary, Config(), new Budget()).Query("Synthetic blue bottle"));
            Assert.Equal("invalid_embedding", failure.Code);
        }
    }
}
