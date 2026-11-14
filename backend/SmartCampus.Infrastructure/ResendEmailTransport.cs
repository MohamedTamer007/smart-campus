using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MimeKit;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>Sends transactional mail through the fixed Resend HTTPS endpoint.</summary>
public sealed class ResendEmailTransport(IHttpClientFactory clients, IConfiguration config)
{
    public async Task<string> Send(string recipient, string subject, string link, string idempotencyKey, CancellationToken cancellation = default)
    {
        var key = config["Email:ResendKey"];
        var sender = config["Email:From"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(sender))
            throw new RuleException("email_not_configured", 503);
        if (!MailboxAddress.TryParse(sender, out var address)) throw new RuleException("email_not_configured", 503);
        var testRecipient = config["Email:TestRecipient"];
        var sharedTestSender = address.Address.EndsWith("@resend.dev", StringComparison.OrdinalIgnoreCase);
        if (sharedTestSender && string.IsNullOrWhiteSpace(testRecipient))
            throw new RuleException("email_test_recipient_required", 503);
        if (sharedTestSender && !string.IsNullOrWhiteSpace(testRecipient))
        {
            var allowed = Rules.UniversityEmail(testRecipient);
            if (!allowed.Equals(recipient, StringComparison.OrdinalIgnoreCase)) throw new RuleException("email_recipient_not_allowed", 503);
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        var content = CampusEmailContent.Create(subject, link);
        request.Content = JsonContent.Create(new { from = sender, to = new[] { recipient }, subject, text = content.Text, html = content.Html });
        using var client = clients.CreateClient("email");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new RuleException("email_rate_limited", 503);
        if (!response.IsSuccessStatusCode)
            throw new RuleException((int)response.StatusCode is >= 400 and < 500 ? "email_rejected" : "email_unavailable", 503);
        // Provider error bodies can contain recipients and message contents; never include them in exceptions/logs.
        using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        var bytes = new byte[4097];
        var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellation);
        if (count == bytes.Length) throw new RuleException("invalid_email_response", 503);
        try
        {
            using var document = JsonDocument.Parse(bytes.AsMemory(0, count));
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length > 200)
                throw new RuleException("invalid_email_response", 503);
            return id.GetString()!;
        }
        catch (JsonException) { throw new RuleException("invalid_email_response", 503); }
    }
}
