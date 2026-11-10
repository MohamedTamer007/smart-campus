# Smart Campus — Render Free real-app deployment

Status: **repository prepared, not deployed**. The public MonsterASP.NET
`/preview/` page is a separate synthetic-only demo, not the application
described here.

## Target architecture (no paid subscriptions)

- Render Free **one Docker web service** from `render.yaml`: the actual
  ASP.NET Core 10 backend and the regular React frontend, served on a
  single Render-managed HTTPS `*.onrender.com` origin. The service has
  512 MiB RAM and a limited CPU; verify real memory and cold startup.
- Existing Supabase Free (project `mninpcrhqsaafoshjqpn`) for
  PostgreSQL 17, pgvector, durable session key material, mail queue and
  private media. **Do not create Render Free PostgreSQL**: it expires
  after 30 days, and the existing Supabase project already contains
  the isolated application schema and retained legacy data.
- Resend HTTPS API for identity verification/recovery, only once an
  authorized sending address is available. **Do not use SMTP ports
  25/465/587** from Render Free; the provider blocks them.
- Existing OpenRouter/NVIDIA maintenance AI integrations are opt-in and
  budget limited. The current SigLIP lost-and-found search **still
  requires a separate loopback Python worker**. The Docker service
  deliberately does *not* bundle that large worker. Search stays
  unavailable until a privacy-reviewed remote embeddings adapter
  replaces it and passes calibration tests. Never claim that an
  unconfigured API key is enough to provide working search.

## Prepare and connect Render

1. Connect the available **Render** integration in ChatGPT, or register at
   https://dashboard.render.com using the **Free** workspace. Linking the
   private GitHub repository requires its owner's authorization. The
   assistant cannot bypass the Render signup and repository OAuth.
2. In Render, create **New -> Blueprint** from the private
   `MohamedTamer007/smart-campus` repository, selecting branch
   `codex/backend-integration` and root `render.yaml`. It defines
   **one** web service, `runtime: docker`, `plan: free`, region
   Frankfurt, `autoDeployTrigger: off`, and no database/disk/worker
   resources. Check the displayed plan is Free before proceeding.
3. Render will offer a managed HTTPS `onrender.com` URL. Confirm its
   **actual** hostname; do not guess. Set `PublicOrigin` to
   `https://ACTUAL_HOST.onrender.com/` and `AllowedHosts` to
   `ACTUAL_HOST.onrender.com` in the private Render environment
   variables screen (names already declared `sync: false`).
4. Configure the remaining secrets **inside Render**, never in GitHub
   commits, source files, screenshots, build artifacts or this chat.
   `ConnectionStrings__Campus` must use the verified-TLS Supabase
   session pooler and a dedicated no-DDL runtime role with access only
   to `smart_campus` and permitted `extensions`. Do not use the
   Supabase migration/admin role or expose the app schema through the
   Data API.
5. Use an approved private Supabase Storage API key in
   `Storage__Key`; keep both existing buckets private. Inspect and
   correct the old authenticated-wide read policy before uploading
   real application data under the `smart-campus/` prefix.
6. Supply authorized `Email__From` and `Email__ResendKey`
   (HTTPS transport) plus real approved `Release__AllowedEmails__0`.
   The shared Resend test domain does not permit arbitrary university
   recipients. The code refuses real registration and jobs without a
   working, permitted sender.
7. Configure `DataProtection__Store=database` (already in the
   Blueprint), a long-lived private X.509 PKCS#12 certificate encoded
   as Base64 in `DataProtection__CertificateBase64` and its private
   password in `DataProtection__CertificatePassword`. Safely back up
   the certificate and its password **outside** Render: losing them
   makes existing encrypted session and email records unreadable.
   This differs from storing an XML key file on the ephemeral Render
   filesystem. The keys reside encrypted inside Supabase's isolated
   `smart_campus."DataProtectionKeys"` table.
8. Apply the reviewed, **non-destructive ninth EF migration**
   `20261110225500_RenderPersistentKeys` using a separate migration
   identity. The public application does **not** auto-migrate.
   A runtime role only needs `SELECT,INSERT` on the new protected
   key table; it must not gain DDL, SUPERUSER or BypassRLS. Verify
   that the original 26 application tables and all legacy schema
   records remain untouched.
9. Only then manually deploy the Render web service and test
   `/health/live`, `/health/ready` (database check),
   registration -> actual delivered verification email -> sign-in ->
   maintenance submit/track, session continuity after restart and
   authorized private media. `/health/ready` is NOT a substitute
   for complete acceptance.

## Edge HTTPS and ephemeral filesystem

Render terminates TLS at its managed edge and redirects public HTTP
requests to HTTPS. The app's `Hosting__Platform=render` configuration
avoids an internal ASP.NET redirect loop, while preserving secure
cookies, origin validation and HSTS. No arbitrary forwarded proxy
headers are trusted by this configuration.

Render Free offers no persistent disk. We added
`PostgresKeyRepository` for certificate-encrypted ASP.NET Core
Data Protection key-ring persistence, with a migration and
restart-regression test. No persistent local directories are used for
the Render path. User uploads must remain in private Supabase Storage.

## Unresolved and explicit restrictions

- Render Free spins down after inactivity; accepted as a temporary
  project constraint, but outbox/background work pauses while idle.
- The platform provides 750 free instance-hours per workspace/month
  and has other bandwidth/build quotas. Watch usage: **zero subscription
  price does not imply unlimited traffic or an absolute zero-cost
  guarantee**. Do not enable any paid plan, disk, job or add-on.
- Managed PostgreSQL runtime credentials, host certificate, approved
  sender, privacy-sensitive storage policy, validated campus directory,
  controlled admin identity, AI-provider keys and search calibration
  are not yet configured. They must not be fabricated.
- The app includes an in-process JobWorker. No second Render Free
  background worker and no always-running guarantees were added.
- Full official student operation remains unaccepted until real
  email/identity/privacy/backup and end-to-end tests pass.

Render references:
- https://render.com/docs/free
- https://render.com/docs/web-services
- https://render.com/docs/blueprint-spec
- https://render.com/docs/configure-environment-variables
