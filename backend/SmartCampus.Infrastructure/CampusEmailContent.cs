using System.Net;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Responsive, bilingual transactional email content. All dynamic URLs are HTML-encoded;
/// no untrusted HTML or account data is interpolated into the template.
/// </summary>
public static class CampusEmailContent
{
    public sealed record Message(string Html, string Text);

    public static Message Create(string subject, string link)
    {
        var reset = subject == "Smart Campus password reset";
        var title = reset ? "إعادة تعيين كلمة المرور" : "تأكيد البريد الإلكتروني";
        var englishTitle = reset ? "Reset your password" : "Verify your email";
        var message = reset
            ? "وصلنا طلب لإعادة تعيين كلمة مرور حسابك. اضغط على الزر التالي لاختيار كلمة مرور جديدة."
            : "أهلاً بيك في Smart Campus! اضغط على الزر التالي لتأكيد بريدك الإلكتروني وتفعيل حسابك.";
        var english = reset
            ? "We received a request to reset your password."
            : "Welcome to Smart Campus! Verify your email to activate your account.";
        var button = reset ? "تغيير كلمة المرور" : "تأكيد البريد الإلكتروني";
        var buttonEnglish = reset ? "Reset password" : "Verify email";
        var safeLink = WebUtility.HtmlEncode(link);
        const string template = """
<!doctype html>
<html lang="ar" dir="rtl">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{EN_TITLE}}</title></head>
<body style="margin:0;padding:0;background:#f2f5fa;color:#14223a;font-family:Tahoma,Arial,sans-serif;">
  <div style="display:none;font-size:1px;color:#f2f5fa;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">{{AR_TITLE}} - Smart Campus</div>
  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#f2f5fa;">
    <tr><td align="center" style="padding:36px 16px;">
      <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width:560px;background:#ffffff;border:1px solid #e5ebf2;border-radius:18px;overflow:hidden;">
        <tr><td style="background:#10233e;padding:30px 34px;text-align:right;">
          <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
            <td style="background:#20c8a8;color:#10233e;padding:10px 12px;border-radius:10px;font-size:18px;font-weight:800;letter-spacing:1px;">SC</td>
            <td style="padding-right:13px;color:#ffffff;text-align:right;"><span style="font-size:22px;font-weight:800;letter-spacing:.2px;">Smart Campus</span><div style="color:#bdcbdc;font-size:12px;padding-top:4px;">Your campus, connected</div></td>
          </tr></table>
        </td></tr>
        <tr><td style="height:5px;background:#20c8a8;font-size:1px;line-height:5px;">&nbsp;</td></tr>
        <tr><td style="padding:38px 34px 20px;text-align:right;">
          <div style="font-size:12px;font-weight:700;letter-spacing:1px;color:#159a80;">SMART CAMPUS &nbsp; / &nbsp; ACCOUNT SECURITY</div>
          <h1 style="margin:18px 0 10px;color:#10233e;font-size:26px;line-height:1.5;">{{AR_TITLE}}</h1>
          <p dir="ltr" style="margin:0 0 24px;color:#65778f;font-size:14px;line-height:1.8;text-align:right;">{{EN_TITLE}}</p>
          <p style="margin:0 0 12px;color:#34465e;font-size:16px;line-height:2;">{{MESSAGE}}</p>
          <p dir="ltr" style="margin:0 0 30px;color:#728199;font-size:13px;line-height:1.7;text-align:right;">{{ENGLISH}}</p>
          <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center"><tr><td bgcolor="#129d85" style="border-radius:10px;text-align:center;">
            <a href="{{LINK}}" style="display:inline-block;background:#129d85;color:#ffffff;text-decoration:none;font-weight:700;font-size:16px;line-height:22px;padding:16px 30px;border-radius:10px;">{{BUTTON}}</a>
          </td></tr></table>
          <p dir="ltr" style="text-align:center;color:#63738a;margin:14px 0 0;font-size:12px;">{{BUTTON_EN}}</p>
        </td></tr>
        <tr><td style="padding:8px 34px 34px;text-align:right;">
          <p style="font-size:13px;line-height:1.9;color:#66758a;margin:18px 0 6px;">لو الزر مش شغال، انسخ الرابط ده وافتحه في المتصفح:</p>
          <p dir="ltr" style="font-size:11px;line-height:1.7;word-break:break-all;overflow-wrap:anywhere;text-align:left;margin:0;">
            <a href="{{LINK}}" style="color:#137d71;text-decoration:underline;">{{LINK}}</a>
          </p>
          <div style="border-top:1px solid #e5ebf2;margin-top:28px;padding-top:20px;color:#718096;font-size:12px;line-height:1.9;">
            لو ماطلبتش الرسالة دي، تقدر تتجاهلها. متشاركش الرابط مع أي حد.<br>
            If you didn't request this, you can safely ignore this email.
          </div>
        </td></tr>
      </table>
      <p style="font-size:12px;color:#8d99aa;margin:20px 0 0;text-align:center;">Smart Campus &nbsp;•&nbsp; Student project</p>
    </td></tr>
  </table>
</body>
</html>
""";
        var html = template
            .Replace("{{LINK}}", safeLink)
            .Replace("{{AR_TITLE}}", title)
            .Replace("{{EN_TITLE}}", englishTitle)
            .Replace("{{MESSAGE}}", message)
            .Replace("{{ENGLISH}}", english)
            .Replace("{{BUTTON_EN}}", buttonEnglish)
            .Replace("{{BUTTON}}", button);
        var text = $"Smart Campus\n\n{title}\n{englishTitle}\n\n{message}\n{english}\n\n{button}:\n{link}\n\nلو ماطلبتش الرسالة دي، تجاهلها. متشاركش الرابط مع أي حد.\nIf you didn't request this, ignore this email.";
        return new Message(html, text);
    }
}
