using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class ProviderTests
{
    private sealed class ProviderBoundary(HttpStatusCode status, string body) : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
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
        var adapter = new AiProviders(provider, Config());
        var failure = await Assert.ThrowsAsync<RuleException>(() => adapter.Assess("Synthetic leak near wiring", CancellationToken.None));
        Assert.Equal(expected, failure.Code);
    }

    [Fact]
    public async Task Valid_provider_envelope_returns_only_validated_suggestions()
    {
        const string body = """{"choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Water near wiring\"}"}}]}""";
        using var provider = new ProviderBoundary(HttpStatusCode.OK, body);
        var suggestion = await new AiProviders(provider, Config()).Assess("Synthetic leak", CancellationToken.None);
        Assert.Equal("plumbing", suggestion.Type);
        Assert.Equal("urgent", suggestion.Priority);
        Assert.Equal("high", suggestion.Risk);
    }
}
