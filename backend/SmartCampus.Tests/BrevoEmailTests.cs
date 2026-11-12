using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class BrevoEmailTests
{
    private sealed class Provider : HttpMessageHandler, IHttpClientFactory
    {
        public string? Body { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Url { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri?.ToString();
            ApiKey = request.Headers.TryGetValues("api-key", out var keys) ? keys.Single() : null;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created) {
                Content = new StringContent("{\"messageId\":\"test-provider-id\"}")
            };
        }
    }

    [Theory]
    [InlineData("Verify your Smart Campus email", "تأكيد البريد الإلكتروني", "Verify email")]
    [InlineData("Smart Campus password reset", "إعادة تعيين كلمة المرور", "Reset password")]
    public async Task Branded_html_has_secure_action_and_text_fallback(string subject, string heading, string button)
    {
        using var provider = new Provider();
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Email:BrevoKey"] = "synthetic-test-api-key",
            ["Email:From"] = "Smart Campus <sender@example.test>"
        }).Build();
        var transport = new BrevoEmailTransport(provider, settings);
        const string link = "https://campus.example.test/verify?userId=synthetic&token=synthetic";
        var id = await transport.Send("student@o6u.edu.eg", subject, link, "smart-campus/email/" + Guid.NewGuid().ToString("N"));
        Assert.Equal("test-provider-id", id);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", provider.Url);
        Assert.Equal("synthetic-test-api-key", provider.ApiKey);
        using var data = JsonDocument.Parse(provider.Body!);
        var root = data.RootElement;
        var html = root.GetProperty("htmlContent").GetString()!;
        var text = root.GetProperty("textContent").GetString()!;
        Assert.Contains(heading, html);
        Assert.Contains(button, html);
        Assert.Contains("Smart Campus", html);
        Assert.Contains("https://campus.example.test/verify?userId=synthetic&amp;token=synthetic", html);
        Assert.Contains(link, text);
        Assert.Contains("Student project", html);
        Assert.Equal("student@o6u.edu.eg", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal(subject, root.GetProperty("subject").GetString());
    }

    [Fact]
    public void Email_body_does_not_allow_html_injection_through_link()
    {
        var mail = CampusEmailContent.Create("Verify your Smart Campus email",
            "https://campus.example.test/verify?token=abc\"<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", mail.Html);
        Assert.Contains("&lt;script&gt;", mail.Html);
        Assert.Contains("&quot;", mail.Html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:student@o6u.edu.eg")]
    [InlineData("https://user:password@campus.example.test/verify")]
    public void Email_action_rejects_non_web_or_credential_bearing_urls(string link) =>
        Assert.Throws<ArgumentException>(() => CampusEmailContent.Create("Verify your Smart Campus email", link));
}
