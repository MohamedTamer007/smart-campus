# Approved decisions and current dependencies

The addendum authorizes safe development, feature-branch commits/pushes, a review PR, and initial free testing deployment after required provider setup. It does not authorize paid plans, protected-branch merges, destructive migrations, consequential external permission changes, or a release to real university users.

## Product and architecture

- Preserve the existing React design, Arabic default and English support.
- Use .NET 10, Identity site passwords, PostgreSQL, server-backed cookie sessions and CSRF protection.
- Keep React and the API on one HTTPS origin. The previously proposed GitHub Pages URL returned 404 during the deployment audit.
- Preserve Supabase project `mninpcrhqsaafoshjqpn`, legacy public tables/trial records and private buckets. Application migrations own the private `smart_campus` schema; startup never migrates it.
- Keep the six approved workflow decisions: five qualifying results maximum; no new requests while reserved; only the current custodian may cancel approval before either confirmation; no transfer while reserved; completed recovery closes competing requests with `ItemRecovered`; maintenance cancellation is a separate timestamp.
- Retain the approved Nemotron/OpenRouter, optional explicit text-only NVIDIA alternative, and pinned SigLIP worker. No paid fallback or invented predictions.
- Search remains disabled until reviewed real-model calibration approves filtering.

## Hosting and email clarification

The user initially selected Oracle Always Free and a controlled sending domain, then clarified that no custom domain or Oracle account currently exists. Use a free HTTPS hostname; do not purchase a domain or activate paid resources.

Oracle remains a compute candidate for the full worker, subject to account setup, free capacity and Arm runtime verification. Render/Koyeb free web instances have insufficient memory for the pinned worker. A complete hosted architecture has not yet been provisioned. Do not describe the prepared Dockerfiles as a deployed service.

The Resend HTTPS adapter is implemented and tested against an isolated HTTP boundary, but Resend requires sender-domain verification. Real delivery is blocked without a verified sender. Microsoft Graph delegated `Mail.Send` is a possible university-mail option, subject to tenant policy and explicit consent. No Graph adapter or consent grant has been implemented. Possession of an Outlook mailbox alone does not establish app permission.

Clarification for the initial single-inbox proof: Resend's shared `onboarding@resend.dev` test sender can deliver only to its account email. An owner-created free Resend account tied to the authorized university test address can therefore test real verification/recovery delivery to that inbox without a custom domain. This is not general university delivery and does not remove the independent-account, sender-authorization or full-pilot gates. No message or account creation has occurred.

## Approved testing targets

Target ten simultaneous signed-in users, warm ordinary API p95 below two seconds, RPO 24 hours and RTO four hours. AI latency and cold starts are measured separately. These are targets, not achieved performance or recovery results.

Verified campus directory/routing, maintenance taxonomy, approved calibration, authorized university test mailbox, provider credentials and hosting access remain external inputs. Credentials belong in local/provider/CI secret storage, never ordinary chat or Git.
