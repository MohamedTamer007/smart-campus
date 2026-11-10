# Real verification-mail test

Resend tools were not exposed in this Codex session; the plugin catalog search also returned no Resend entry. The application therefore uses its existing backend HTTPS adapter. A ChatGPT connection does not supply an API key to this backend.

## Secure local setup

1. In the account owner's [Resend API Keys page](https://resend.com/api-keys), create a **Sending access** key for this test. Do not paste it into chat, a shell command, source files or screenshots. The account email must be the authorized university test mailbox: Resend's shared test sender cannot deliver to other addresses.
2. Run `scripts/start-resend-test.ps1 -AuthorizedEmail 'your-authorized-address@o6u.edu.eg' -DotnetPath 'PATH_TO_DOTNET_10'`. The task also provides a machine-specific `Start-Resend-Test.cmd` outside Git with the owner's approved address and SDK path already selected.
3. Enter the key at the hidden prompt. The launcher sets it only in the current process environment inherited by the API; it writes no key file or command-line key. Leave the window running. Ctrl+C stops the API and restores the parent environment.
4. The launcher requires the existing loopback `_dev` database and a free port 5080, selects `onboarding@resend.dev`, disables the local mail sink, restricts release/outbound testing to the authorized address, and keeps external AI and uncalibrated search disabled. Only one local API/job worker may run against this test configuration.

This first delivery test runs the real Resend transport with the local development database. The verification link points to `http://127.0.0.1:5080` and must be opened on this PC while the launcher is running. This is not the public HTTPS deployment or multi-user university delivery. A temporary Cloudflare Quick Tunnel is available for a limited cross-device test if needed, but its random hostname is not stable and its docs limit it to testing/development. Do not use it to declare public deployment complete.

The script supports `-ValidateOnly` for SDK/database/port preflight without requesting a key, starting the server or sending mail. Preflight passed under Windows PowerShell 5.1 and PowerShell 7. The eighth migration, `EmailProviderEvidence`, adds nullable provider correlation/failure fields and must be applied locally before this test. Hosted migrations remain separate operator actions.

## Execute and verify

Use the actual backend registration endpoint to create an ordinary **unverified** test account. An existing unverified account uses resend verification instead. Do not modify `EmailConfirmed` directly or extract the token from the local outbox as a substitute for real delivery. The owner confirms inbox receipt and opens the actual mail link. Recovery is exercised only after verification, with the owner choosing the new site password.

The outbox retains `ProviderMessageId` after Resend returns a validated success ID. `SentAt` records transport acceptance, **not delivery**. The encrypted recipient/link payload is cleared after acceptance. Rejections/unavailability retain a sanitized `FailureCode` and do not set acceptance metadata. The test-domain adapter refuses missing/mismatched `Email:TestRecipient` before contacting Resend.

Match the provider ID with the owner's Resend dashboard email/event record. A **Sending access** key can only send; do not request full account API access merely to read delivery logs. The owner can sign into the dashboard for the read-only log check. Record only provider ID, recipient, subject/time and event status; never copy API keys or verification/reset tokens into reports.

| Evidence | Meaning |
|---|---|
| Queued job, no provider ID | Not yet confirmed accepted by Resend |
| Valid provider ID and transport timestamp | Accepted by Resend; delivery unproven |
| Provider `delivered` event | Recipient mail server accepted it; owner inbox/link confirmation still needed |
| Provider `bounced`/`failed` event | Delivery failed; report the actual event |
| `email_rejected` without acceptance metadata | Provider rejected the send request |
| Timeout/unavailable/invalid response | Acceptance/delivery unconfirmed; do not report success |

Current state: transport/recipient/evidence tests passed, launcher preflight passed, and the local migration is prepared/applied. **No real email has been sent yet: the private key entry is pending.**

Sources: [Resend test-domain restriction](https://resend.com/docs/knowledge-base/403-error-resend-dev-domain), [API key permissions](https://resend.com/docs/api-reference/api-keys/create-api-key), [email retrieval/events](https://resend.com/docs/api-reference/emails/retrieve-email).
