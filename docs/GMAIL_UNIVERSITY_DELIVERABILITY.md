# Transactional email to o6u.edu.eg: Gmail SMTP + Outlook junk triage

## Scope and status

Smart Campus currently sends account verification and password reset mail to university addresses using Gmail SMTP, with `Email:Provider=smtp`. This change makes SMTP messages use the existing bilingual, branded `CampusEmailContent` template as proper `multipart/alternative` (HTML and plain text), and names the sender Smart Campus when only an address is configured. It does **not** guarantee Inbox placement or alter the university's Microsoft 365 tenant policy. The Gmail SMTP account is `smarttcampus@gmail.com` (never send as `@o6u.edu.eg` without authorization). No customer credentials or verification/reset tokens belong in Git.

## Before any university-wide invitation

1. Keep `Email__Provider=smtp`, `Email__Host=smtp.gmail.com`, `Email__Port=587`, `Email__LocalSink=false`, and set both `Email__Username` and the address in `Email__From` to the authenticated Gmail account. Use only the App Password in the hosting secret configuration, not source control. Avoid editing environment values unless needed.
2. Deploy the reviewed code only after tests and explicit approval. The existing `Release__Mode=university` enables the backend's `@o6u.edu.eg` address restriction; changing email providers must not disable email confirmation before sign-in.
3. Send a new verification email to an authorized university mailbox, then test a password reset separately. Check the *Inbox and Junk*, subject, display name, branded HTML, plain-text fallback, link origin, and activation/reset flow. Do not publish or forward a working token link.
4. Inspect the received message headers (`Authentication-Results`) for `spf`, `dkim`, `dmarc`, and `compauth` statuses and the tenant's spam verdict (`SCL`), using the Outlook/Defender message details or the university administrator's Exchange message trace. Do not assume authentication fails just because the message is in Junk. If SPF/DKIM/DMARC fails, diagnose forwarding or From/SMTP mismatch before any allow rule.
5. Ask the university Microsoft 365 mail administrator to review the **specific** legitimate Smart Campus message at [Defender > Submissions](https://security.microsoft.com/reportsubmission) as **good email that should not have been blocked**. They can inspect authentication, policy hits, URL reputation, and, where warranted, create a narrowly scoped/temporary allow entry for the verified sender in the Tenant Allow/Block List. Do not allowlist all of `gmail.com`, disable anti-spam, or bypass phishing/malware checks. Only authorized tenant admins can make organization-wide changes.
6. Retest with multiple authorized student mailboxes after the university's verdict/changes and record the result; some mailbox-specific filtering can still apply. Gmail consumer account quotas and abuse controls mean this is not an unlimited campus-wide mailing service.

## External sources

- Microsoft: https://learn.microsoft.com/en-us/defender-office-365/create-safe-sender-lists-in-office-365
- Microsoft: https://learn.microsoft.com/en-us/microsoft-365/security/office-365-security/submissions-admin
- Microsoft: https://learn.microsoft.com/en-us/defender-office-365/email-authentication-troubleshoot
- Google: https://support.google.com/mail/answer/81126
- Gmail SMTP: https://developers.google.com/workspace/gmail/imap/imap-smtp

## Validation record

Code-only change prepared, not deployed from this branch. Static review and unit tests added; running .NET tests requires a .NET 10 SDK in an authorized checkout/CI. **No Inbox acceptance or SPF/DKIM/DMARC result has been verified by this change.**
