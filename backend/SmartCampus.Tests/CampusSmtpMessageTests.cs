using MimeKit;
using MimeKit.Text;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class CampusSmtpMessageTests
{
    [Theory]
    [InlineData("Verify your Smart Campus email", "تأكيد البريد الإلكتروني")]
    [InlineData("Smart Campus password reset", "إعادة تعيين كلمة المرور")]
    public void Gmail_smtp_uses_branded_html_and_readable_text_for_both_account_flows(string subject, string arabicHeading)
    {
        const string link = "https://smartcampus.tryasp.net/verify?userId=synthetic&token=synthetic";
        var message = CampusSmtpMessage.Create("smarttcampus@gmail.com", "student@o6u.edu.eg", subject, link);

        Assert.Equal(subject, message.Subject);
        var from = Assert.IsType<MailboxAddress>(Assert.Single(message.From.Mailboxes));
        Assert.Equal("Smart Campus", from.Name);
        Assert.Equal("smarttcampus@gmail.com", from.Address);
        Assert.Equal("student@o6u.edu.eg", Assert.Single(message.To.Mailboxes).Address);
        Assert.IsType<MultipartAlternative>(message.Body);

        var plain = message.GetTextBody(TextFormat.Plain);
        var html = message.GetTextBody(TextFormat.Html);
        Assert.NotNull(plain);
        Assert.NotNull(html);
        Assert.Contains(arabicHeading, plain);
        Assert.Contains(link, plain);
        Assert.Contains(arabicHeading, html);
        Assert.Contains("Smart Campus", html);
        Assert.Contains("https://smartcampus.tryasp.net/verify?userId=synthetic&amp;token=synthetic", html);
        Assert.DoesNotContain("<script>", html);
        // HTML email has exactly one visible action: the branded button.
        // Its URL stays inside href, never printed as a long link below it.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html!, "<a href="));
        Assert.DoesNotContain(">{{LINK}}</a>", html);
        Assert.DoesNotContain(">https://", html);
        Assert.DoesNotContain("انسخ الرابط", html);
    }

    [Fact]
    public void Configured_sender_display_name_is_preserved_without_spoofing_a_domain()
    {
        var message = CampusSmtpMessage.Create(
            "Smart Campus notifications <smarttcampus@gmail.com>",
            "another.student@o6u.edu.eg",
            "Verify your Smart Campus email",
            "https://smartcampus.tryasp.net/verify?token=synthetic");
        var from = Assert.Single(message.From.Mailboxes);
        Assert.Equal("Smart Campus notifications", from.Name);
        Assert.Equal("smarttcampus@gmail.com", from.Address);
    }
}
