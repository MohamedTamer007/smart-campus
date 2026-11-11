using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class OptionalEmailProviderTests
{
    private sealed record Captured(Uri Url, string Body, string? ApiKey);
    private sealed class Boundary(HttpStatusCode status, string body) : HttpMessageHandler, IHttpClientFactory
    {
        public List<Captured> Calls { get; } = [];
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(new(request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken),
                request.Headers.TryGetValues("api-key", out var values) ? values.Single() : null));
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static IConfiguration EmailJsConfig(string? secret = "private-synthetic-emailjs") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Email:EmailJsServiceId"] = "service_test", ["Email:EmailJsTemplateId"] = "template_test",
            ["Email:EmailJsPublicKey"] = "public-synthetic-emailjs", ["Email:EmailJsPrivateKey"] = secret
        }).Build();

    private static IConfiguration BrevoConfig(string? key = "private-synthetic-brevo", string? from = "Smart Campus <verify@example.test>") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Email:BrevoKey"] = key, ["Email:From"] = from
        }).Build();

    [Fact]
    public async Task EmailJS_uses_backend_fixed_endpoint_and_templated_verified_link()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, "OK");
        const string link = "https://campus.example.test/verify?token=synthetic-secret";
        await new EmailJsEmailTransport(boundary, EmailJsConfig())
            .Send("student@o6u.edu.eg", "Verify Smart Campus", link);
        var request = Assert.Single(boundary.Calls);
        Assert.Equal("https://api.emailjs.com/api/v1.0/email/send", request.Url.ToString());
        using var data = JsonDocument.Parse(request.Body);
        Assert.Equal("service_test", data.RootElement.GetProperty("service_id").GetString());
        Assert.Equal("template_test", data.RootElement.GetProperty("template_id").GetString());
        Assert.Equal("public-synthetic-emailjs", data.RootElement.GetProperty("user_id").GetString());
        Assert.Equal("private-synthetic-emailjs", data.RootElement.GetProperty("accessToken").GetString());
        var variables = data.RootElement.GetProperty("template_params");
        Assert.Equal("student@o6u.edu.eg", variables.GetProperty("to_email").GetString());
        Assert.Equal("Verify Smart Campus", variables.GetProperty("subject").GetString());
        Assert.Equal(link, variables.GetProperty("link").GetString());
    }

    [Fact]
    public async Task Missing_EmailJS_credentials_fail_before_network()
    {
        using var boundary = new Boundary(HttpStatusCode.OK, "OK");
        var error = await Assert.ThrowsAsync<RuleException>(() =>
            new EmailJsEmailTransport(boundary, EmailJsConfig(null))
                .Send("student@o6u.edu.eg", "Verify", "https://example.test/verify"));
        Assert.Equal("email_not_configured", error.Code);
        Assert.Empty(boundary.Calls);
    }

    [Theory]
    [InlineData(429, "email_rate_limited")]
    [InlineData(422, "email_rejected")]
    [InlineData(503, "email_unavailable")]
    public async Task EmailJS_http_failures_have_no_secret_or_provider_body(int status, string expected)
    {
        using var boundary = new Boundary((HttpStatusCode)status, "synthetic-private-link");
        var error = await Assert.ThrowsAsync<RuleException>(() =>
            new EmailJsEmailTransport(boundary, EmailJsConfig())
                .Send("student@o6u.edu.eg", "Verify", "https://example.test/verify"));
        Assert.Equal(expected, error.Code);
        Assert.DoesNotContain("synthetic-private-link", error.ToString());
    }

    [Fact]
    public async Task Brevo_stores_real_message_id_from_fixed_api_and_preserves_outbox_correlation()
    {
        using var boundary = new Boundary(HttpStatusCode.Created, """{"messageId":"<synthetic-brevo-message@relay.test>"}""");
        const string link = "https://campus.example.test/reset/secret-test-token";
        const string idempotency = "smart-campus/email/synthetic-job";
        var id = await new BrevoEmailTransport(boundary, BrevoConfig())
            .Send("student@o6u.edu.eg", "Reset", link, idempotency);
        Assert.Equal("<synthetic-brevo-message@relay.test>", id);
        var request = Assert.Single(boundary.Calls);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", request.Url.ToString());
        Assert.Equal("private-synthetic-brevo", request.ApiKey);
        using var data = JsonDocument.Parse(request.Body);
        Assert.Equal("Smart Campus", data.RootElement.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("verify@example.test", data.RootElement.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("student@o6u.edu.eg", data.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Reset", data.RootElement.GetProperty("subject").GetString());
        Assert.Equal(link, data.RootElement.GetProperty("textContent").GetString());
        Assert.Equal(idempotency, data.RootElement.GetProperty("headers").GetProperty("IdempotencyKey").GetString());
    }

    [Theory]
    [InlineData(null, "Smart Campus <verify@example.test>")]
    [InlineData("", "Smart Campus <verify@example.test>")]
    [InlineData("synthetic-key", null)]
    [InlineData("synthetic-key", "unparseable")]
    public async Task Brevo_rejects_missing_key_or_invalid_sender_without_network(string? key, string? sender)
    {
        using var boundary = new Boundary(HttpStatusCode.Created, "{}");
        var error = await Assert.ThrowsAsync<RuleException>(() =>
            new BrevoEmailTransport(boundary, BrevoConfig(key, sender))
                .Send("student@o6u.edu.eg", "Verify", "https://example.test/verify", "synthetic-key"));
        Assert.Equal("email_not_configured", error.Code);
        Assert.Empty(boundary.Calls);
    }

    [Theory]
    [InlineData(429, "email_rate_limited")]
    [InlineData(401, "email_rejected")]
    [InlineData(422, "email_rejected")]
    [InlineData(500, "email_unavailable")]
    public async Task Brevo_provider_errors_are_sanitized(int status, string expected)
    {
        using var boundary = new Boundary((HttpStatusCode)status, "secret-token-in-provider-error");
        var error = await Assert.ThrowsAsync<RuleException>(() =>
            new BrevoEmailTransport(boundary, BrevoConfig())
                .Send("student@o6u.edu.eg", "Verify", "https://example.test/verify", "synthetic-key"));
        Assert.Equal(expected, error.Code);
        Assert.DoesNotContain("secret-token-in-provider-error", error.ToString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("{\"messageId\":\"\"}")]
    [InlineData("{\"messageId\":null}")]
    public async Task Brevo_invalid_success_body_is_not_acknowledged(string body)
    {
        using var boundary = new Boundary(HttpStatusCode.Created, body);
        var error = await Assert.ThrowsAsync<RuleException>(() =>
            new BrevoEmailTransport(boundary, BrevoConfig())
                .Send("student@o6u.edu.eg", "Verify", "https://example.test/verify", "synthetic-key"));
        Assert.Equal("invalid_email_response", error.Code);
    }
}
