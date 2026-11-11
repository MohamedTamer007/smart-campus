using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MimeKit;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Server-only Brevo transactional mail adapter. Never exposes the API key,
/// recipient or verification/reset link in exceptions or application logs.
/// A provider message ID means accepted for processing, not mailbox delivery.
/// </summary>
public sealed class BrevoEmailTransport(IHttpClientFactory clients, IConfiguration config)
{
    public async Task<string> Send(string recipient, string subject, string link, string idempotencyKey, CancellationToken cancellation = default)
    {
        var apiKey = config["Email:BrevoKey"];
        var sender = config["Email:From"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(sender) ||
            !MailboxAddress.TryParse(sender, out var mailbox) ||
            !System.Net.Mail.MailAddress.TryCreate(mailbox.Address, out var parsedAddress) ||
            !parsedAddress.Address.Contains('@'))
            throw new RuleException("email_not_configured", 503);

        // Brevo requires a UUID-format idempotency key. The durable outbox
        // already uses a stable UUID (in N format); retain it across retries.
        var rawJobId = idempotencyKey[(idempotencyKey.LastIndexOf('/') + 1)..];
        if (!Guid.TryParse(rawJobId, out var jobId) || jobId == Guid.Empty)
            throw new RuleException("invalid_email_idempotency", 503);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        var content = CampusEmailContent.Create(subject, link);
        request.Content = JsonContent.Create(new
        {
            sender = new { name = string.IsNullOrWhiteSpace(mailbox.Name) ? "Smart Campus" : mailbox.Name, email = mailbox.Address },
            to = new[] { new { email = recipient } },
            subject,
            textContent = link,
            // This key is already stable for a queued email. Brevo may use it
            // for correlating/retries, but it does not replace outbox deduplication.
            headers = new Dictionary<string, string> { ["IdempotencyKey"] = jobId.ToString("D") }
        });
        using var client = clients.CreateClient("email");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new RuleException("email_rate_limited", 503);
        if (!response.IsSuccessStatusCode)
            throw new RuleException((int)response.StatusCode is >= 400 and < 500 ? "email_rejected" : "email_unavailable", 503);

        using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        var bytes = new byte[4097];
        var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellation);
        if (count == bytes.Length) throw new RuleException("invalid_email_response", 503);
        try
        {
            using var document = JsonDocument.Parse(bytes.AsMemory(0, count));
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("messageId", out var id) ||
                id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) ||
                id.GetString()!.Length > 200)
                throw new RuleException("invalid_email_response", 503);
            return id.GetString()!;
        }
        catch (JsonException) { throw new RuleException("invalid_email_response", 503); }
    }
}
