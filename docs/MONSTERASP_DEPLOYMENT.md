# MonsterASP.NET: verified packaging and release gates (2026-11-10)

## Current status

The user has an existing MonsterASP.NET onboarding email and a website-creation confirmation, but **no MonsterASP.NET management/WebDeploy/SFTP connector is authorized in this workspace**. An account-confirmation email is not hosting API access or evidence of a successful site deployment. Do not extract/share publish passwords from email or source-control them.

The existing public/static Cloudflare Pages site remains separate. The repository's default `main` branch is not replaced by this preparation.

A reproducible Windows IIS package workflow is available at
`.github/workflows/monsterasp-package.yml`. It builds the React app and the
.NET 10 release API, combines the frontend under `wwwroot`, checks for
`web.config`/the IIS hosting module and missing private-key artifacts,
then uploads a short-retention ZIP-compatible artifact from GitHub Actions.
**This is preparation only**; it does not activate a website, set secrets, or
open registration to university users.

## Why the free host alone is not the final architecture

MonsterASP.NET supports Windows/IIS ASP.NET Core deployments. The existing
Smart Campus solution also requires a separate Python inference worker
(`worker/`) running a pinned SigLIP model, with persistent model cache and
access to the API only over a private/shared loopback network. MonsterASP.NET
Free shared IIS hosting is not a drop-in Python container/always-running
worker host. The frontend and .NET API *might* be hosted here after secure
configuration and measurement; the complete AI search experience cannot
be enabled there without a separate reviewed worker host and a secure
connection. Do not publicly expose the worker API or embed its secret in React.

Additionally, the .NET app currently has a long-lived hosted background
service (`JobWorker`), persistent ASP.NET Data Protection encryption keys,
a mandatory production hosting security validator, PostgreSQL certificate
verification, private media, and a controlled-release email transport.
A free IIS web app pool may recycle or idle, so cron-like delivery,
retries and worker processing cannot be claimed reliable without a verified
separate background-processing arrangement.

## Supported deployment approach

1. Build `monsterasp-windows-publish-<SHA>` with the GitHub Actions workflow.
2. Owner reviews the selected website, free-tier resource limits, .NET 10
   runtime/architecture, HTTPS certificate, host recycle policy and whether
   `DataProtection__Directory` and an encryption certificate can be stored
   with durable access restrictions. The existing environment sample in
   `deployment/api.env.example` documents required settings; **do not upload
   that sample as a live `.env` file**.
3. Use MonsterASP.NET Control Panel -> Websites -> selected site -> Deploy.
   If WebDeploy is enabled, store the site/server/username/password in
   GitHub repository secrets rather than the repository. This workspace has
   no authority to set those secrets. Do not commit a publish profile or FTP
   password, even to a private repository. Alternatively upload the
   reviewed publish package using the provider's secure authenticated UI.
   Host-side automatic Git deploy on push is restricted by provider plan;
   GitHub Actions may perform build/deploy separately with proper access.
4. Configure non-development `PublicOrigin` and `AllowedHosts` for the
   actual HTTPS hostname. Set up an appropriately scoped PostgreSQL runtime
   role; do not use an admin/migration role for web requests.
5. Obtain a verified private storage credential and review/apply the
   prepared storage prefix restriction after backup/approval. Keep image
   upload/search gated until this is finished.
6. Configure approved sender and recipient list for a controlled
   registration/verification/recovery test; no email provider account or
   university tenant authorization is inferred from mailbox access.
7. Confirm IIS process starts; check `/health/live`, `/health/ready`, the
   HTML/JS app, account verification/recovery, and real browser workflows
   with scoped users. Validate session persistence across app-pool recycle,
   scheduled processing under idle/scale-down, private media rules and
   actual delivered mail before any real release.
8. Only after the above, provision an appropriately secured independent
   inference worker and approve search calibration. This is outside the
   standalone MonsterASP.NET Free host.

## Operational and ownership boundaries

The Supabase private `smart_campus` schema was installed on 2026-11-10,
with 26 tables and eight EF migration records; the 38 legacy public trial
records were preserved. No full external API or site has been deployed.
Never put production secrets into the uploaded GitHub Actions artifacts
or run integration tests against the shared Supabase production project.

The free site may not suit complete production service availability; if
app-pool recycle, model memory, disk, key persistence, HTTPS provisioning
or transport restrictions fail an acceptance requirement, report the
limit rather than disabling security and misrepresenting a preview as
100% functional.

Official provider references:
- https://help.monsterasp.net/websites/git-deploy/github-actions
- https://help.monsterasp.net/websites/deploy/visual-studio
- https://www.monsterasp.net/GitHub-Deploy/
- https://www.monsterasp.net/ASP.NET-Freehosting/
