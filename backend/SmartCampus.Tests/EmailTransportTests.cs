using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class EmailTransportTests
{
    private sealed record CapturedRequest(string Url, string? Authorization, string IdempotencyKey, string Body);
    private sealed class EmailBoundary(HttpStatusCode status, string body) : HttpMessageHandler, IHttpClientFactory
    {
        public List<CapturedRequest> Requests { get; } = [];
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.GetValues("Idempotency-Key").Single(), await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new(status) { Content = new StringContent(body) };
        }
    }

    private static IConfiguration Config(string? key = "test-only-email-key", string? sender = "Smart Campus <verification@example.test>") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Email:ResendKey"] = key, ["Email:From"] = sender
        }).Build();

    [Fact]
    public async Task Resend_sends_only_expected_fields_and_preserves_retry_identity()
    {
        using var provider = new EmailBoundary(HttpStatusCode.OK, "{\"id\":\"test-message-id\"}");
        var transport = new ResendEmailTransport(provider, Config());
        const string link = "https://campus.example.test/verify?token=synthetic-token";
        var messageId = await transport.Send("student@o6u.edu.eg", "Verify Smart Campus", link, "smart-campus/email/test-id");
        Assert.Equal("test-message-id", messageId);
        await transport.Send("student@o6u.edu.eg", "Verify Smart Campus", link, "smart-campus/email/test-id");
        Assert.Equal(2, provider.Requests.Count);
        Assert.All(provider.Requests, request => {
            Assert.Equal("https://api.resend.com/emails", request.Url);
            Assert.Equal("Bearer test-only-email-key", request.Authorization);
            Assert.Equal("smart-campus/email/test-id", request.IdempotencyKey);
            using var document = JsonDocument.Parse(request.Body);
            Assert.Equal(4, document.RootElement.EnumerateObject().Count());
            Assert.Equal("Smart Campus <verification@example.test>", document.RootElement.GetProperty("from").GetString());
            Assert.Equal("student@o6u.edu.eg", document.RootElement.GetProperty("to")[0].GetString());
            Assert.Equal("Verify Smart Campus", document.RootElement.GetProperty("subject").GetString());
            Assert.Equal(link, document.RootElement.GetProperty("text").GetString());
        });
    }

    [Theory]
    [InlineData(null, "owner@o6u.edu.eg", "email_test_recipient_required")]
    [InlineData("owner@o6u.edu.eg", "other@o6u.edu.eg", "email_recipient_not_allowed")]
    public async Task Shared_sender_never_contacts_provider_without_the_authorized_test_recipient(string? allowed, string recipient, string expected)
    {
        var config = Config(sender: "Smart Campus testing <onboarding@resend.dev>");
        config["Email:TestRecipient"] = allowed;
        using var provider = new EmailBoundary(HttpStatusCode.OK, "{\"id\":\"test-message-id\"}");
        var failure = await Assert.ThrowsAsync<RuleException>(() => new ResendEmailTransport(provider, config)
            .Send(recipient, "Verify", "https://example.test/verify", "test-id"));
        Assert.Equal(expected, failure.Code);
        Assert.Empty(provider.Requests);
    }

    [Fact]
    public async Task Shared_sender_accepts_only_the_configured_university_test_recipient()
    {
        var config = Config(sender: "Smart Campus testing <onboarding@resend.dev>");
        config["Email:TestRecipient"] = "OWNER@o6u.edu.eg";
        using var provider = new EmailBoundary(HttpStatusCode.OK, "{\"id\":\"test-message-id\"}");
        Assert.Equal("test-message-id", await new ResendEmailTransport(provider, config)
            .Send("owner@o6u.edu.eg", "Verify", "https://example.test/verify", "test-id"));
        Assert.Single(provider.Requests);
    }

    [Theory]
    [InlineData(null, "verification@example.test")]
    [InlineData("", "verification@example.test")]
    [InlineData("test-only-email-key", null)]
    [InlineData("test-only-email-key", " ")]
    public async Task Unconfigured_email_never_contacts_provider(string? key, string? sender)
    {
        using var provider = new EmailBoundary(HttpStatusCode.OK, "{}");
        var failure = await Assert.ThrowsAsync<RuleException>(() => new ResendEmailTransport(provider, Config(key, sender))
            .Send("student@o6u.edu.eg", "Verify", "https://example.test", "test-id"));
        Assert.Equal("email_not_configured", failure.Code);
        Assert.Empty(provider.Requests);
    }

    [Theory]
    [InlineData(429, "email_rate_limited")]
    [InlineData(401, "email_rejected")]
    [InlineData(422, "email_rejected")]
    [InlineData(503, "email_unavailable")]
    public async Task Email_provider_failures_are_classified_without_leaking_response(int status, string expected)
    {
        using var provider = new EmailBoundary((HttpStatusCode)status, "private-recipient-and-token");
        var failure = await Assert.ThrowsAsync<RuleException>(() => new ResendEmailTransport(provider, Config())
            .Send("student@o6u.edu.eg", "Verify", "https://example.test", "test-id"));
        Assert.Equal(expected, failure.Code);
        Assert.DoesNotContain("private-recipient-and-token", failure.ToString());
        Assert.Single(provider.Requests);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    [InlineData("{\"id\":\" \"}")]
    public async Task Invalid_email_success_response_is_rejected(string body)
    {
        using var provider = new EmailBoundary(HttpStatusCode.OK, body);
        var failure = await Assert.ThrowsAsync<RuleException>(() => new ResendEmailTransport(provider, Config())
            .Send("student@o6u.edu.eg", "Verify", "https://example.test", "test-id"));
        Assert.Equal("invalid_email_response", failure.Code);
    }

    [Fact]
    public async Task Oversized_email_response_is_rejected()
    {
        using var provider = new EmailBoundary(HttpStatusCode.OK, new string(' ', 4097));
        var failure = await Assert.ThrowsAsync<RuleException>(() => new ResendEmailTransport(provider, Config())
            .Send("student@o6u.edu.eg", "Verify", "https://example.test", "test-id"));
        Assert.Equal("invalid_email_response", failure.Code);
    }
}
