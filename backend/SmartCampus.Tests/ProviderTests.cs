using System.Net;
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
        public Task Reserve(string provider, CancellationToken cancellation) => Task.CompletedTask;
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
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["AI:AllowNonSensitiveInference"] = "true", ["AI:OpenRouterKey"] = "test-only-not-a-real-key", ["AI:FaultTypes:0"] = "plumbing",
        ["AI:Priorities:0"] = "urgent", ["AI:Risks:0"] = "high"
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
    public async Task NVIDIA_rejection_status_is_classified_without_exposing_details(int status, string expected)
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
        Assert.Equal(0, route.GetProperty("max_price").GetProperty("prompt").GetInt32());
        Assert.Equal(0, route.GetProperty("max_price").GetProperty("completion").GetInt32());
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
    public async Task Auto_routes_reviewed_text_to_Nvidia_GPT_OSS_without_image()
    {
        var config = AutoConfig();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        await new AiProviders(provider, config, new AvailableTestBudget()).Assess("Synthetic leak", CancellationToken.None);
        Assert.Equal("openai/gpt-oss-20b", provider.Model);
        Assert.Equal("https://integrate.api.nvidia.com/v1/chat/completions", provider.Destination!.ToString());
        Assert.Equal("low", provider.Sent!.Value.GetProperty("reasoning_effort").GetString());
        Assert.False(provider.Sent!.Value.TryGetProperty("response_format", out _));
        Assert.False(provider.Sent!.Value.GetProperty("stream").GetBoolean());
        Assert.True(AiProviders.Configured(config));
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
    public async Task Auto_routes_reviewed_image_to_OpenRouter_Super_VL()
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


    [Theory]
    [InlineData("auto", true, true)]
    [InlineData("openrouter", true, true)]
    [InlineData("nvidia", true, false)]
    [InlineData("auto", false, false)]
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
    public void Image_review_does_not_bypass_general_inference_or_nvidia_quota_gates()
    {
        var settings = AutoConfig().AsEnumerable().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        settings["AI:AllowNonSensitiveInference"] = "false";
        Assert.False(AiProviders.CanReviewImage(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));

        settings["AI:AllowNonSensitiveInference"] = "true";
        settings["AI:NvidiaDailyRequestLimit"] = "0";
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
        settings["AI:Provider"] = "auto";
        settings["AI:NvidiaKey"] = "synthetic-nvidia-test-key";
        settings["AI:NvidiaDailyRequestLimit"] = "3";
        settings["AI:NvidiaRequestsPerMinute"] = "5";
        settings["AI:AllowNonSensitiveImages"] = "true";
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }


    [Fact]
    public async Task Nvidia_fenced_JSON_is_parsed_only_if_all_labels_are_allowed()
    {
        var envelope = JsonSerializer.Serialize(new {
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
