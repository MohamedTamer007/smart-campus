using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class ProviderTests
{
    private sealed class AvailableTestBudget : IInferenceBudget
    {
        public int RequestsReserved { get; private set; }
        public Task Reserve(string provider, CancellationToken cancellation)
        {
            RequestsReserved++;
            return Task.CompletedTask;
        }
    }
    private sealed class ExhaustedTestBudget : IInferenceBudget
    {
        public Task Reserve(string provider, CancellationToken cancellation) => throw new RuleException("ai_quota_exhausted", 503, DateTimeOffset.UtcNow.AddDays(1));
    }
    private sealed class ProviderBoundary(HttpStatusCode status, string body) : HttpMessageHandler, IHttpClientFactory
    {
        public int Requests { get; private set; }
        public string? Model { get; private set; }
        public JsonElement? Sent { get; private set; }
        public Uri? Destination { get; private set; }
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Destination = request.RequestUri;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Model = payload.RootElement.GetProperty("model").GetString();
            Sent = payload.RootElement.Clone();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class EmbeddingsBoundary : HttpMessageHandler, IHttpClientFactory
    {
        public int Requests { get; private set; }
        public JsonElement? Sent { get; private set; }
        public Uri? Destination { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("retrieval", name);
            return new HttpClient(this, false);
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Destination = request.RequestUri;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Sent = payload.RootElement.Clone();
            var vector = Enumerable.Range(0, RemoteEmbeddings.Dimension).Select(index => index == 0 ? 1f : 0f).ToArray();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { embedding = new { values = vector } }) };
        }
    }
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["AI:AllowNonSensitiveInference"] = "true", ["AI:ProviderDataCollection"] = "allow",
        ["AI:OpenRouterKey"] = "test-only-not-a-real-key", ["AI:FaultTypes:0"] = "plumbing",
        ["AI:Priorities:0"] = "urgent", ["AI:Risks:0"] = "high"
    }).Build();

    private static IConfiguration HostedSearchConfig(bool approveImages = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Search:Provider"] = "hosted",
        ["Search:ExternalProcessingApproved"] = "true",
        ["Search:GeminiExternalProcessingApproved"] = "true",
        ["Search:ExternalImagesApproved"] = approveImages.ToString(),
        ["Search:GeminiExternalImagesApproved"] = approveImages.ToString(),
        ["Search:GeminiProviderDataCollection"] = "allow",
        ["AI:GeminiKey"] = "test-only-not-a-real-key",
        ["AI:GeminiFreeTierOnly"] = "true",
        ["AI:GeminiDailyRequestLimit"] = "50",
        ["AI:GeminiRequestsPerMinute"] = "10"
    }).Build();

    private sealed class ThrowingProvider(Exception failure) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(failure);
    }

    [Fact]
    public async Task Failed_https_request_returns_sanitized_rule_error()
    {
        using var provider = new ThrowingProvider(new HttpRequestException("Synthetic SSL/TLS failure, never a real provider secret"));
        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, AutoConfig(), new AvailableTestBudget())
            .Assess("TEST ONLY imaginary pipe", CancellationToken.None));
        Assert.Equal("ai_connection_failed", failure.Code);
        Assert.Equal(503, failure.Status);
    }

    [Fact]
    public async Task Provider_timeout_returns_sanitized_rule_error()
    {
        using var provider = new ThrowingProvider(new TaskCanceledException("Synthetic provider timeout"));
        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, AutoConfig(), new AvailableTestBudget())
            .Assess("TEST ONLY imaginary pipe", CancellationToken.None));
        Assert.Equal("ai_timeout", failure.Code);
        Assert.Equal(503, failure.Status);
    }

    [Theory]
    [InlineData(400, "ai_request_rejected")]
    [InlineData(401, "ai_key_rejected")]
    [InlineData(403, "ai_access_denied")]
    public async Task OpenRouter_rejection_status_is_classified_without_exposing_details(int status, string expected)
    {
        using var provider = new ProviderBoundary((HttpStatusCode)status, "{\"secret\":\"must never be returned\"}");
        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, AutoConfig(), new AvailableTestBudget())
            .Assess("TEST ONLY imaginary leak", CancellationToken.None));
        Assert.Equal(expected, failure.Code);
        Assert.Equal(503, failure.Status);
    }

    [Theory]
    [InlineData(429, "{}", "ai_rate_limited")]
    [InlineData(503, "{}", "ai_unavailable")]
    [InlineData(200, "{}", "invalid_ai_response")]
    public async Task Provider_failure_never_creates_a_suggestion(int status, string body, string expected)
    {
        using var provider = new ProviderBoundary((HttpStatusCode)status, body);
        var adapter = new AiProviders(provider, Config(), new AvailableTestBudget());
        var failure = await Assert.ThrowsAsync<RuleException>(() => adapter.Assess("Synthetic leak near wiring", CancellationToken.None));
        Assert.Equal(expected, failure.Code);
    }

    [Fact]
    public async Task Valid_provider_envelope_returns_only_validated_suggestions()
    {
        const string body = """{"model":"nvidia/nemotron-3.5-super-vl:free","choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Water near wiring\"}"}}]}""";
        using var provider = new ProviderBoundary(HttpStatusCode.OK, body);
        var suggestion = await new AiProviders(provider, Config(), new AvailableTestBudget()).Assess("Synthetic leak", CancellationToken.None);
        Assert.Equal("plumbing", suggestion.Type);
        Assert.Equal("urgent", suggestion.Priority);
        Assert.Equal("high", suggestion.Risk);
    }

    [Fact]
    public async Task OpenRouter_text_classification_uses_pinned_free_model_without_paid_fallback()
    {
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        var suggestion = await new AiProviders(provider, Config(), new AvailableTestBudget())
            .Assess("TEST ONLY. Fictional pipe leak in an empty room.", CancellationToken.None);

        Assert.Equal("plumbing", suggestion.Type);
        Assert.Equal("urgent", suggestion.Priority);
        Assert.Equal("high", suggestion.Risk);
        Assert.Equal("nvidia/nemotron-3.5-super-vl:free", provider.Model);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", provider.Destination!.ToString());
        var payload = provider.Sent!.Value;
        Assert.Equal(JsonValueKind.String, payload.GetProperty("messages")[1].GetProperty("content").ValueKind);
        Assert.Equal("json_object", payload.GetProperty("response_format").GetProperty("type").GetString());
        var route = payload.GetProperty("provider");
        Assert.False(route.GetProperty("allow_fallbacks").GetBoolean());
        Assert.Equal("allow", route.GetProperty("data_collection").GetString());
        Assert.Equal(0, route.GetProperty("max_price").GetProperty("prompt").GetInt32());
        Assert.Equal(0, route.GetProperty("max_price").GetProperty("completion").GetInt32());
    }

    [Theory]
    [InlineData("deny")]
    [InlineData(null)]
    public async Task Unapproved_provider_data_collection_blocks_maintenance_inference_before_budget_or_http(string? policy)
    {
        var settings = Config().AsEnumerable().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        settings["AI:ProviderDataCollection"] = policy;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var budget = new AvailableTestBudget();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);

        Assert.False(AiProviders.Configured(config));
        Assert.False(AiProviders.CanReviewImage(config));
        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, config, budget)
            .Assess("TEST ONLY fictional pipe leak", CancellationToken.None));

        Assert.Equal("ai_not_configured", failure.Code);
        Assert.Equal(0, budget.RequestsReserved);
        Assert.Equal(0, provider.Requests);
    }

    [Fact]
    public async Task OpenRouter_text_classification_rejects_a_non_free_response_model()
    {
        const string paidModelResponse = """{"model":"nvidia/nemotron-3.5-super-vl","choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Synthetic\"}"}}]}""";
        using var provider = new ProviderBoundary(HttpStatusCode.OK, paidModelResponse);

        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, Config(), new AvailableTestBudget())
            .Assess("TEST ONLY. Fictional pipe leak in an empty room.", CancellationToken.None));

        Assert.Equal("invalid_ai_response", failure.Code);
        Assert.Equal(1, provider.Requests);
    }

    [Fact]
    public async Task Legacy_auto_routing_is_rejected_without_a_provider_request()
    {
        var settings = AutoConfig().AsEnumerable().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        settings["AI:Provider"] = "auto";
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, config, new AvailableTestBudget())
            .Assess("TEST ONLY synthetic leak", CancellationToken.None));
        Assert.Equal("ai_not_configured", failure.Code);
        Assert.False(AiProviders.Configured(config));
        Assert.Equal(0, provider.Requests);
    }

    private static byte[] SyntheticPng()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        return encoded.ToArray();
    }

    [Fact]
    public async Task OpenRouter_routes_reviewed_image_to_free_Super_VL()
    {
        var config = AutoConfig();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        await new AiProviders(provider, config, new AvailableTestBudget())
            .Assess("Synthetic broken pipe", CancellationToken.None, SyntheticPng());
        Assert.Equal("nvidia/nemotron-3.5-super-vl:free", provider.Model);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", provider.Destination!.ToString());
        var content = provider.Sent!.Value.GetProperty("messages")[1].GetProperty("content");
        var url = content[1].GetProperty("image_url").GetProperty("url").GetString();
        Assert.StartsWith("data:image/jpeg;base64,", url);
        using var pixels = SKData.CreateCopy(Convert.FromBase64String(url!["data:image/jpeg;base64,".Length..]));
        using var codec = SKCodec.Create(pixels);
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec!.EncodedFormat);
    }

    [Fact]
    public async Task OpenRouter_resizes_and_compresses_legacy_large_image_before_reserving_and_sending()
    {
        const int width = 2048;
        const int height = 1536;
        using var bitmap = new SKBitmap(width, height);
        var random = new Random(42);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var original = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        Assert.True(original!.Size > CampusImageNormalizer.MaximumNormalizedBytes);

        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        var budget = new AvailableTestBudget();
        await new AiProviders(provider, AutoConfig(), budget).Assess("Synthetic legacy photo", CancellationToken.None, original.ToArray());

        Assert.Equal(1, provider.Requests);
        Assert.Equal(1, budget.RequestsReserved);
        var content = provider.Sent!.Value.GetProperty("messages")[1].GetProperty("content");
        var url = content[1].GetProperty("image_url").GetProperty("url").GetString()!;
        Assert.StartsWith("data:image/jpeg;base64,", url);
        var normalized = Convert.FromBase64String(url["data:image/jpeg;base64,".Length..]);
        Assert.InRange(normalized.Length, 1, CampusImageNormalizer.MaximumNormalizedBytes);
        using var normalizedData = SKData.CreateCopy(normalized);
        using var codec = SKCodec.Create(normalizedData);
        Assert.True(Math.Max(codec!.Info.Width, codec.Info.Height) <= CampusImageNormalizer.MaximumEdge);
    }

    [Fact]
    public async Task Hosted_image_embedding_uses_Gemini_768_dimension_endpoint_and_inline_image()
    {
        var config = HostedSearchConfig();
        using var provider = new EmbeddingsBoundary();
        var embedding = await new RemoteEmbeddings(provider, config, new AvailableTestBudget()).Image(SyntheticPng());

        Assert.Equal(RemoteEmbeddings.Dimension, embedding.Dimension);
        Assert.Equal(RemoteEmbeddings.Endpoint, provider.Destination!.ToString());
        var payload = provider.Sent!.Value;
        Assert.Equal(768, payload.GetProperty("outputDimensionality").GetInt32());
        Assert.Equal("image/jpeg", payload.GetProperty("content").GetProperty("parts")[0].GetProperty("inline_data").GetProperty("mime_type").GetString());
    }

    [Fact]
    public async Task Hosted_image_embedding_without_external_image_approval_sends_no_request()
    {
        using var provider = new EmbeddingsBoundary();
        var error = await Assert.ThrowsAsync<RuleException>(() => new RemoteEmbeddings(provider,
            HostedSearchConfig(approveImages: false), new AvailableTestBudget()).Image(SyntheticPng()));
        Assert.Equal("search_external_images_not_approved", error.Code);
        Assert.Equal(0, provider.Requests);
    }


    [Theory]
    [InlineData("openrouter", true, true)]
    [InlineData("nvidia", true, false)]
    [InlineData("auto", true, false)]
    [InlineData("openrouter", false, false)]
    public void Staff_image_review_gate_matches_selected_provider_and_explicit_image_approval(
        string selectedProvider, bool imagesApproved, bool expected)
    {
        var settings = AutoConfig().AsEnumerable().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        settings["AI:Provider"] = selectedProvider;
        settings["AI:AllowNonSensitiveImages"] = imagesApproved.ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Assert.Equal(expected, AiProviders.CanReviewImage(config));
    }

    [Fact]
    public void Image_review_does_not_bypass_general_inference_or_openrouter_quota_gates()
    {
        var settings = AutoConfig().AsEnumerable().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        settings["AI:AllowNonSensitiveInference"] = "false";
        Assert.False(AiProviders.CanReviewImage(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));

        settings["AI:AllowNonSensitiveInference"] = "true";
        settings["AI:OpenRouterDailyRequestLimit"] = "0";
        Assert.False(AiProviders.CanReviewImage(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
    }

    [Fact]
    public async Task Invalid_approved_image_is_rejected_before_reserving_or_sending()
    {
        var config = AutoConfig();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        var error = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, config, new AvailableTestBudget())
            .Assess("TEST ONLY pipe", CancellationToken.None, [0xff, 0xd8, 0xff, 0xd9]));
        Assert.Equal("invalid_upload", error.Code);
        Assert.Equal(0, provider.Requests);
    }

    [Fact]
    public async Task Unapproved_image_is_rejected_before_an_external_provider_request()
    {
        var settings = AutoConfig().AsEnumerable().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        settings["AI:AllowNonSensitiveImages"] = "false";
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        var error = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, config, new AvailableTestBudget())
            .Assess("TEST ONLY pipe", CancellationToken.None, [0xff, 0xd8, 0xff, 0xd9]));
        Assert.Equal("ai_images_not_permitted", error.Code);
        Assert.Equal(0, provider.Requests);
    }

    private const string ValidEnvelope = """{"model":"nvidia/nemotron-3.5-super-vl:free","choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Synthetic\"}"}}]}""";

    private static IConfiguration AutoConfig()
    {
        var settings = Config().AsEnumerable().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        settings["AI:Provider"] = "openrouter";
        settings["AI:OpenRouterDailyRequestLimit"] = "50";
        settings["AI:OpenRouterRequestsPerMinute"] = "20";
        settings["AI:AllowNonSensitiveImages"] = "true";
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }


    [Fact]
    public async Task OpenRouter_fenced_JSON_is_parsed_only_if_all_labels_are_allowed()
    {
        var envelope = JsonSerializer.Serialize(new {
            model = "nvidia/nemotron-3.5-super-vl:free",
            choices = new[] { new {
                finish_reason = "stop",
                message = new { content = "```json\n{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"TEST ONLY\"}\n```" }
            }}
        });
        using var provider = new ProviderBoundary(HttpStatusCode.OK, envelope);
        var suggestion = await new AiProviders(provider, AutoConfig(), new AvailableTestBudget())
            .Assess("TEST ONLY imaginary leak", CancellationToken.None);
        Assert.Equal("plumbing", suggestion.Type);
        Assert.Equal("urgent", suggestion.Priority);
    }

    [Fact]
    public async Task Exhausted_budget_prevents_external_inference()
    {
        using var provider = new ProviderBoundary(HttpStatusCode.OK, "{}");
        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, Config(), new ExhaustedTestBudget())
            .Assess("Synthetic leak", CancellationToken.None));
        Assert.Equal("ai_quota_exhausted", failure.Code);
        Assert.Equal(0, provider.Requests);
    }
}
