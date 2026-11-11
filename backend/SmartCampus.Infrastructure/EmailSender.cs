using MailKit.Net.Smtp;
using MailKit.Security;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MimeKit;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed class EmailSender(IConfiguration config, IHostEnvironment environment, CampusDb db, IDataProtectionProvider protection, ResendEmailTransport resend, EmailJsEmailTransport emailJs, BrevoEmailTransport brevo, ReleasePolicy release)
{
    private readonly IDataProtector protector = protection.CreateProtector("SmartCampus.OutboundEmail.v1");
    private sealed record EmailPayload(string Recipient, string Subject, string Link);
    public async Task Enqueue(string recipient, string subject, string link)
    {
        if (environment.IsDevelopment() && config.GetValue<bool>("Email:LocalSink")) { await Send(recipient, subject, link); return; }
        var email = new OutboundEmail { ProtectedPayload = protector.Protect(JsonSerializer.Serialize(new EmailPayload(recipient, subject, link))) };
        db.OutboundEmails.Add(email);
        db.Jobs.Add(new() { Kind = "email", EntityId = email.Id, Key = "email:" + email.Id });
        await db.SaveChangesAsync();
    }

    public async Task Deliver(Guid id)
    {
        var email = await db.OutboundEmails.FindAsync(id) ?? throw new RuleException("email_unavailable", 503);
        if (email.SentAt.HasValue) return;
        var payload = JsonSerializer.Deserialize<EmailPayload>(protector.Unprotect(email.ProtectedPayload))!;
        release.Require(payload.Recipient);
        try
        {
            email.ProviderMessageId = await Send(payload.Recipient, payload.Subject, payload.Link, "smart-campus/email/" + email.Id.ToString("N"));
            email.FailureCode = null;
        }
        catch (Exception exception) when (exception is RuleException or HttpRequestException or TaskCanceledException)
        {
            email.FailureCode = exception is RuleException rule ? rule.Code : "email_unavailable";
            throw;
        }
        email.SentAt = DateTimeOffset.UtcNow;
        email.ProtectedPayload = "";
    }

    public async Task<string?> Send(string recipient, string subject, string link, string? idempotencyKey = null)
    {
        if (config["Email:Provider"] == "resend" && !(environment.IsDevelopment() && config.GetValue<bool>("Email:LocalSink")))
        {
            return await resend.Send(recipient, subject, link, idempotencyKey ?? "smart-campus/email/" + Guid.NewGuid().ToString("N"));
        }
        if (config["Email:Provider"] == "emailjs" && !(environment.IsDevelopment() && config.GetValue<bool>("Email:LocalSink")))
        {
            await emailJs.Send(recipient, subject, link);
            return null; // EmailJS returns acceptance only, not a provider message ID.
        }
        if (config["Email:Provider"] == "brevo" && !(environment.IsDevelopment() && config.GetValue<bool>("Email:LocalSink")))
            return await brevo.Send(recipient, subject, link, idempotencyKey ?? "smart-campus/email/" + Guid.NewGuid().ToString("N"));
        var sender = config["Email:From"];
        if (string.IsNullOrWhiteSpace(sender))
        {
            if (!environment.IsDevelopment()) throw new RuleException("email_not_configured", 503);
            sender = "Smart Campus development <no-reply@example.test>";
        }
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(sender));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = link };
        if (environment.IsDevelopment() && config.GetValue<bool>("Email:LocalSink"))
        {
            var directory = Path.GetFullPath(config["Email:SinkDirectory"] ?? "local-data/mail");
            Directory.CreateDirectory(directory);
            await message.WriteToAsync(Path.Combine(directory, Guid.NewGuid() + ".eml"));
            return null;
        }
        var host = config["Email:Host"];
        var username = config["Email:Username"];
        if (string.IsNullOrWhiteSpace(host)) throw new RuleException("email_unavailable", 503);
        using var client = new SmtpClient { Timeout = 30000 };
        await client.ConnectAsync(host, config.GetValue("Email:Port", 587), SecureSocketOptions.StartTls);
        if (!string.IsNullOrEmpty(username))
            await client.AuthenticateAsync(username, config["Email:Password"] ?? throw new RuleException("email_unavailable", 503));
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
        return null;
    }
}
