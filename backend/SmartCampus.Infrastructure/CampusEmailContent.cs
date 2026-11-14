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
        if (!Uri.TryCreate(link, UriKind.Absolute, out var target) || target.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(target.UserInfo))
            throw new ArgumentException("The email action link must be an absolute HTTP(S) URL without user info.", nameof(link));
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
        var safeLogoUrl = WebUtility.HtmlEncode(target.GetLeftPart(UriPartial.Authority) + "/o6u-mark.png");
        const string template = """
<!doctype html>
<html lang="ar" dir="rtl">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="color-scheme" content="light dark"><title>{{EN_TITLE}}</title></head>
<body style="margin:0;padding:0;background:#f5f8fc;color:#142b54;font-family:'Noto Sans Arabic',Tahoma,Arial,sans-serif;">
  <div style="display:none;font-size:1px;color:#f5f8fc;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">{{AR_TITLE}} - Smart Campus</div>
  <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background:#f5f8fc;">
    <tr><td align="center" style="padding:36px 16px;">
      <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="max-width:560px;background:#ffffff;border:1px solid #dce5ef;border-radius:14px;overflow:hidden;">
        <tr><td style="background:#142b54;padding:26px 30px;text-align:right;">
          <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
            <td style="padding:4px 8px 4px 0;"><img src="{{LOGO_URL}}" alt="Smart Campus" width="34" height="34" style="display:block;width:34px;height:34px;object-fit:contain;"></td>
            <td style="padding-right:13px;color:#ffffff;text-align:right;"><span style="font-size:22px;font-weight:800;letter-spacing:.2px;">Smart Campus</span><div style="color:#d9e4f3;font-size:12px;padding-top:4px;">Your campus, connected</div></td>
          </tr></table>
        </td></tr>
        <tr><td style="height:5px;background:#c44300;font-size:1px;line-height:5px;">&nbsp;</td></tr>
        <tr><td style="padding:38px 34px 20px;text-align:right;">
          <div style="font-size:12px;font-weight:700;letter-spacing:1px;color:#144caa;">SMART CAMPUS &nbsp; / &nbsp; ACCOUNT SECURITY</div>
          <h1 style="margin:18px 0 10px;color:#142b54;font-size:26px;line-height:1.5;">{{AR_TITLE}}</h1>
          <p dir="ltr" style="margin:0 0 24px;color:#65778f;font-size:14px;line-height:1.8;text-align:right;">{{EN_TITLE}}</p>
          <p style="margin:0 0 12px;color:#34465e;font-size:16px;line-height:2;">{{MESSAGE}}</p>
          <p dir="ltr" style="margin:0 0 30px;color:#728199;font-size:13px;line-height:1.7;text-align:right;">{{ENGLISH}}</p>
          <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:0 auto;"><tr>
            <td bgcolor="#144caa" align="center" style="background:#144caa;border-radius:10px;border:1px solid #144caa;">
              <a href="{{LINK}}" target="_blank" style="display:inline-block;background:#144caa;color:#ffffff;font-family:Tahoma,Arial,sans-serif;font-size:16px;font-weight:700;line-height:24px;text-align:center;text-decoration:none;padding:16px 34px;border-radius:10px;">{{BUTTON}}</a>
            </td>
          </tr></table>
          <p dir="ltr" style="text-align:center;color:#63738a;margin:12px 0 0;font-size:12px;line-height:1.6;">{{BUTTON_EN}}</p>
        </td></tr>
        <tr><td style="padding:6px 34px 34px;text-align:right;">
          <div style="border-top:1px solid #e5ebf2;margin-top:24px;padding-top:20px;color:#718096;font-size:12px;line-height:1.9;">
            لأمان حسابك، استخدم الزر الموجود بالأعلى فقط. الرابط مخصص لحسابك ولا ينبغي مشاركته.<br>
            For your security, use the button above. This action is intended for your account only.<br><br>
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
            .Replace("{{LOGO_URL}}", safeLogoUrl)
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
