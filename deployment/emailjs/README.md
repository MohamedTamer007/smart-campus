# EmailJS template for account emails

`smart-campus-account-email.html` is the Smart Campus verification / password-reset email
(the same branded, bilingual layout the SMTP, Brevo and Resend providers send from
`CampusEmailContent.cs`), written with EmailJS variables.

## Create the template in EmailJS

1. Email Templates -> Create New Template -> **Code Editor**, paste the HTML file.
2. **To Email** = `{{to_email}}`, **Subject** = `{{subject}}`, **From Name** = `Smart Campus`,
   leave **Reply To**, Bcc and Cc empty.
3. Account -> Security: allow the EmailJS API for non-browser applications and use the private key.
4. Copy the generated Template ID into `Email__EmailJsTemplateId`.

## Variables sent by the backend (`EmailJsEmailTransport`)

`to_email`, `subject`, `link`, `title_ar`, `title_en`, `message_ar`, `message_en`,
`button_ar`, `button_en`, `logo_url`. The wording changes with the subject
(`Smart Campus password reset` = reset, anything else = verification).

## Hosting settings (environment variables, never committed)

`Email__Provider=emailjs`, `Email__EmailJsServiceId`, `Email__EmailJsTemplateId`,
`Email__EmailJsPublicKey`, `Email__EmailJsPrivateKey`, `Email__EmailJsTrialApproved=true`.
EmailJS acceptance (HTTP 200) does not prove inbox delivery; the free plan allows 200 requests a month.
