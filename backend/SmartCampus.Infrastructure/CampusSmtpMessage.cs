using MimeKit;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Builds standards-compliant multipart transactional mail for SMTP transports.
/// Gmail signs outgoing mail; we must not spoof university sender addresses or
/// try to generate our own DKIM headers in the application.
/// </summary>
public static class CampusSmtpMessage
{
    public static MimeMessage Create(string sender, string recipient, string subject, string link)
    {
        var mailbox = MailboxAddress.Parse(sender);
        var message = new MimeMessage();
        // If configuration contains only an email address, identify the project
        // without changing the authenticated Gmail address.
        message.From.Add(string.IsNullOrWhiteSpace(mailbox.Name)
            ? new MailboxAddress("Smart Campus", mailbox.Address)
            : mailbox);
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;

        var content = CampusEmailContent.Create(subject, link);
        message.Body = new BodyBuilder
        {
            TextBody = content.Text,
            HtmlBody = content.Html
        }.ToMessageBody();
        return message;
    }
}
