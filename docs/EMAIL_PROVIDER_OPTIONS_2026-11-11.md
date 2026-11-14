# Smart Campus transactional email options — 2026-11-11

**Status:** source adapters and mock-provider tests on draft PR #2; no new provider has been configured on the live SnapDeploy deployment and no university-mailbox delivery was verified by this change.

## Recommendation

Use **Brevo HTTPS transactional API** for controlled, low-volume tests when a genuinely authorized sender/domain is available, because it accepts server-side API calls over port 443 and provides a provider message ID. Its free tier currently includes up to **300 email sends/day** (combined use; subject to provider terms/account review) and supports transactional messages. Do not claim message acceptance proves inbox delivery.

Official references:
- https://help.brevo.com/hc/en-us/articles/208580669-FAQs-What-are-the-limits-of-the-Free-plan
- https://developers.brevo.com/docs/send-a-transactional-email
- https://help.brevo.com/hc/en-us/articles/12163873383186-Authenticate-your-domain-with-Brevo-Brevo-code-DKIM-DMARC
- https://help.brevo.com/hc/en-us/articles/7924908994450-Send-transactional-emails-using-Brevo-SMTP
- https://www.emailjs.com/docs/rest-api/send/

## Brevo HTTPS API (preferred on container hosts)

1. Register a real sender/domain in Brevo, authorize sending, and complete domain DKIM/DMARC validation. Brevo may replace free/unverified sending addresses with its relay domain; that's not equivalent to an authenticated owned domain. Only add sender-verification approval after provider dashboard confirms readiness.
2. In a protected host-only secret/environment settings screen, set:
   - `Email__Provider=brevo`
   - `Email__From=Smart Campus <verify@YOUR_AUTHORIZED_DOMAIN>`
   - `Email__BrevoKey=<PRIVATE BREVO v3 API KEY>`
   - `Email__BrevoSenderVerified=true` **only after verifying the sender**
   - `Email__LocalSink=false`
3. For the required university-wide account eligibility, set `Release__Mode=university`; every exact `@o6u.edu.eg` address can register and must verify before sign-in. Use `controlled` with an allowlist only for a deliberately restricted test deployment. Do not use a shared test sender for university-wide mail because its recipient restriction does not support delivery to all campus accounts.
4. Trigger a verification or reset to an authorized university mailbox; inspect the Brevo transaction log by provider `messageId` and check Delivered/bounce events and the actual inbox. Don't store or paste activation links in tickets/chat.
5. Confirm reset token/session behavior, throttle, repeated requests and appropriate failures before switching live provider. If sending from SnapDeploy fails, inspect network errors rather than assuming keys are wrong.

Brevo API adapter uses `https://api.brevo.com/v3/smtp/email`, `api-key` in headers, `textContent` in the body, a fixed service endpoint and bounded response parsing. It accepts a **valid UUID** derived from the durable outbox job ID as Brevo's idempotency key, preserving the identifier for retries. Provider TTL is limited; it does not prove indefinite exactly-once delivery. Errors are mapped to sanitized codes.

## Brevo SMTP alternative — no new provider adapter required

The ASP.NET application already supports MailKit STARTTLS on port 587. Configure existing SMTP provider keys privately:

- `Email__Provider=smtp`
- `Email__Host=smtp-relay.brevo.com`
- `Email__Port=587`
- `Email__Username=<BREVO SMTP LOGIN>`
- `Email__Password=<BREVO SMTP KEY>` (NOT the v3 API key)
- `Email__From=<SAME VERIFIED AUTHORIZED SENDER>`

Use the HTTPS API on hosts that block outbound TCP/587. TLS/STARTTLS and real delivery still need verification; not enough to set env vars.

## EmailJS limited trial

EmailJS REST API is supported as an **opt-in experimental backend adapter** only:
`Email__Provider=emailjs`, `Email__EmailJsServiceId`, `Email__EmailJsTemplateId`, `Email__EmailJsPublicKey`, `Email__EmailJsPrivateKey`, and `Email__EmailJsTrialApproved=true` after reviewing and approving a connected mail service.

The EmailJS template must have **To Email = {{to_email}}**, and include **{{subject}}** and **{{link}}**. Generate the token URL only on the server. This provider's HTTP `200 OK` confirms API acceptance but does **not** provide a Brevo-style provider message ID. EmailJS relies on a connected sender and restricts requests to approximately one per second. Do not invoke the API directly from React; don't assume personal Gmail/Outlook is suitable for university-scale transactional delivery.

## Existing Resend option

Keep Resend configured as-is until a successful replacement smoke test: `Email__Provider=resend`, `Email__ResendKey`, `Email__From`. `onboarding@resend.dev` is a testing sender and provider `Sent` is not evidence of university inbox delivery. No automatic failover to an unknown or paid service is enabled.

## AI clarification

`AI__Provider=openrouter` or `AI__Provider=nvidia` selects **one** of the existing maintenance-assessment providers; both keys may be stored securely but are never sent to one another. Automatic fallback is intentionally disabled. This is **separate** from SigLIP2 photo/text similarity retrieval, which requires its own evaluated model and worker and **does not** use the maintenance chat-completions keys as a drop-in replacement.

No live secrets were copied or changed, no production email job was replayed, and no role grants/department rows were created by this provider work.
