using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
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
        public Uri? Destination { get; private set; }
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Destination = request.RequestUri;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Model = payload.RootElement.GetProperty("model").GetString();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["AI:AllowNonSensitiveInference"] = "true", ["AI:OpenRouterKey"] = "test-only-not-a-real-key", ["AI:FaultTypes:0"] = "plumbing",
        ["AI:Priorities:0"] = "urgent", ["AI:Risks:0"] = "high"
    }).Build();

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
        const string body = """{"choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Water near wiring\"}"}}]}""";
        using var provider = new ProviderBoundary(HttpStatusCode.OK, body);
        var suggestion = await new AiProviders(provider, Config(), new AvailableTestBudget()).Assess("Synthetic leak", CancellationToken.None);
        Assert.Equal("plumbing", suggestion.Type);
        Assert.Equal("urgent", suggestion.Priority);
        Assert.Equal("high", suggestion.Risk);
    }

    [Fact]
    public async Task Auto_routes_reviewed_text_to_Nvidia_GPT_OSS_without_image()
    {
        var config = AutoConfig();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        await new AiProviders(provider, config, new AvailableTestBudget()).Assess("Synthetic leak", CancellationToken.None);
        Assert.Equal("openai/gpt-oss-20b", provider.Model);
        Assert.Equal("https://integrate.api.nvidia.com/v1/chat/completions", provider.Destination!.ToString());
        Assert.True(AiProviders.Configured(config));
    }

    [Fact]
    public async Task Auto_routes_reviewed_image_to_OpenRouter_Super_VL()
    {
        var config = AutoConfig();
        using var provider = new ProviderBoundary(HttpStatusCode.OK, ValidEnvelope);
        await new AiProviders(provider, config, new AvailableTestBudget())
            .Assess("Synthetic broken pipe", CancellationToken.None, [0xff, 0xd8, 0xff, 0xd9]);
        Assert.Equal("nvidia/nemotron-3.5-super-vl:free", provider.Model);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", provider.Destination!.ToString());
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

    private const string ValidEnvelope = """{"choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Synthetic\"}"}}]}""";

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
    public async Task Exhausted_budget_prevents_external_inference()
    {
        using var provider = new ProviderBoundary(HttpStatusCode.OK, "{}");
        var failure = await Assert.ThrowsAsync<RuleException>(() => new AiProviders(provider, Config(), new ExhaustedTestBudget())
            .Assess("Synthetic leak", CancellationToken.None));
        Assert.Equal("ai_quota_exhausted", failure.Code);
        Assert.Equal(0, provider.Requests);
    }
}
