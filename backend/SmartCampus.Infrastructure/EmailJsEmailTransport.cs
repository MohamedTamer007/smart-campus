using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Optional server-side EmailJS connector for limited, controlled mail trials.
/// The provider returns a generic 200/OK, not a delivery receipt or durable
/// provider message ID. Never expose these credentials or send from React.
/// </summary>
public sealed class EmailJsEmailTransport(IHttpClientFactory clients, IConfiguration config)
{
    private const string Endpoint = "https://api.emailjs.com/api/v1.0/email/send";

    public async Task Send(string recipient, string subject, string link, string? name = null, CancellationToken cancellation = default)
    {
        var serviceId = config["Email:EmailJsServiceId"];
        var templateId = config["Email:EmailJsTemplateId"];
        var publicKey = config["Email:EmailJsPublicKey"];
        var privateKey = config["Email:EmailJsPrivateKey"];
        if (string.IsNullOrWhiteSpace(serviceId) || string.IsNullOrWhiteSpace(templateId) ||
            string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
            throw new RuleException("email_not_configured", 503);

        // The wording is computed here (same source as the other providers) so the stored EmailJS
        // template stays static: see deployment/emailjs/smart-campus-account-email.html.
        var parts = CampusEmailContent.Describe(subject, link);
        // EmailJS templates are configured by the operator. The recipient must
        // be a fixed {{to_email}} variable rather than a hardcoded test inbox.
        // Registration and password-reset tokens are minted by the API only.
        using var client = clients.CreateClient("email");
        using var response = await client.PostAsJsonAsync(Endpoint, new
        {
            service_id = serviceId,
            template_id = templateId,
            user_id = publicKey,
            accessToken = privateKey,
            template_params = new
            {
                to_email = recipient, to_name = name ?? "", from_name = "Smart Campus", subject, link,
                title_ar = parts.Title, title_en = parts.EnglishTitle, message_ar = parts.Message, message_en = parts.English,
                button_ar = parts.Button, button_en = parts.ButtonEnglish, logo_url = parts.LogoUrl
            }
        }, cancellation);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new RuleException("email_rate_limited", 503);
        if (!response.IsSuccessStatusCode)
            throw new RuleException((int)response.StatusCode is >= 400 and < 500 ? "email_rejected" : "email_unavailable", 503);
        // Successful EmailJS API acceptance does not confirm final mailbox delivery.
        // Never record a fabricated provider message ID.
    }
}
