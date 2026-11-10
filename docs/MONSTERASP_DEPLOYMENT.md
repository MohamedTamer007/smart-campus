# MonsterASP.NET: verified packaging and release gates (2026-11-10)

## Selected MonsterASP.NET website — 10 November 2026

Owner supplied the website URL `http://smartcampus.tryasp.net/`, created for
ASP.NET Core 10. This is the intended MonsterASP.NET free host, **not** a
confirmed running application deployment. GitHub Windows-runner checks on
2026-11-10 confirmed the public HTTP origin returns **200**, but the HTTPS
origin could not be verified, and **the API has not been deployed**. No
public-origin API health or email journeys have been completed.

Before using `PublicOrigin=https://smartcampus.tryasp.net` and
`AllowedHosts=smartcampus.tryasp.net` on the actual host, publish a reviewed
build, arrange HTTPS activation and verify TLS works. MonsterASP.NET documents
that on Free plans HTTPS is enabled by support **after content has been
uploaded** (see
https://help.monsterasp.net/domains-https/https/lets-encrypt).
Do not enable HTTPS redirects before TLS is working.

The existing package-build workflow currently creates a publish artifact
only. The next release gate is to enable WebDeploy under the selected
site's Deploy page, configure scoped WebDeploy credentials directly as
GitHub Actions repository secrets or through an authenticated publishing
channel, and finish the server secret/identity/DB/mail/security setup.
The provider's recommended secret names are `WEBSITE_NAME`,
`SERVER_COMPUTER_NAME`, `SERVER_USERNAME`, `SERVER_PASSWORD`.
See https://help.monsterasp.net/websites/git-deploy/github-actions.
Do not paste credentials in chat, an artifact, a PR, or repository files.

## Verified GitHub-to-MonsterASP.NET publishing connection — 2026-11-10

The owner enabled WebDeploy and stored these GitHub Actions repository secrets:
`WEBSITE_NAME`, `SERVER_COMPUTER_NAME`, `SERVER_USERNAME`, and
`SERVER_PASSWORD`. MonsterASP.NET's panel test reported **OK, connected in
40 ms**. GitHub's Windows runner independently confirmed that all four secrets
were present, TCP port 8172 was reachable, and a real **no-change remote
`msdeploy -verb:sync -whatif` succeeded**:

- [Run 42881358599](https://github.com/MohamedTamer007/smart-campus/actions/runs/42881358599) — connection and non-mutating sync passed
- [Run 42881693164](https://github.com/MohamedTamer007/smart-campus/actions/runs/42881693164) — connection passed and public site returned HTTP 200; HTTPS did not complete a successful check

The original failures were caused by quoting remote MSDeploy destination
arguments in PowerShell/.NET `ProcessStartInfo.ArgumentList`, which changed
the argument's first character to `"` rather than `-`. A fully synthetic
localhost test reproduced this independently of credentials. Passing the
complete destination argument **without nested quote characters** fixed the
parser failure and established remote `-whatif` connectivity.

`.github/workflows/monsterasp-release.yml` now builds the real Windows
IIS app and tests a **no-change deployment plan** from the combined React +
.NET build. A plain Git push **cannot publish**. A future actual upload requires
an explicit manual workflow dispatch with `publish=true`, all three operator
readiness variables set to `true`, and a successful public HTTPS probe:
`MONSTERASP_PUBLICATION_APPROVED`,
`MONSTERASP_RUNTIME_CONFIG_VERIFIED`, and
`MONSTERASP_SECURITY_REVIEW_APPROVED`. These values are operator attestations
and do **not** replace actually verifying the live hosting configuration.
Keep them absent/false until the real service gates have passed. No site files
have been modified through this work.

**Runtime integration still blocked:** the production app will refuse to start
without HTTPS `PublicOrigin`, a TLS-verified restricted PostgreSQL runtime
account, a durable protected key ring and certificate, secured external image
storage, approved working email and explicit authorized university test
accounts. The Python SigLIP worker cannot execute on MonsterASP.NET Free.

## MonsterASP.NET-specific runtime configuration

The provider documents `Websites -> selected website -> Scripting ->
Environment variables` for secrets like `ConnectionStrings__Campus` and
`Email__ResendKey`. Do not inject database passwords, service keys or PFX
passwords into the published `web.config` or an Actions artifact. The host
provides a private directory **beside** `wwwroot` for persisted data. Its
documentation cautions that IIS shared hosting does not support relying on
DPAPI to protect Data Protection keys; the current Smart Campus code
requires a persistent encrypted key ring and private file-based certificate.
A reviewed certificate in the private directory and runtime environment
configuration must be established before starting production. Do not disable
`HostingSecurity.Validate` to get the site to boot.

Free hosting limits are **256 MB** for the whole site, may sleep after three
hours of inactivity, lacks `AlwaysRunning`, and does not provide scheduled
tasks on Free. The existing `JobWorker` needs a separate durable scheduling
arrangement for reliable emails and AI jobs. This host is a potential
controlled .NET/React preview, **not** a demonstrated full deployment.

Provider documentation:
- https://help.monsterasp.net/websites/configuration/environment-variables
- https://help.monsterasp.net/websites/configuration/data-protection-keys
- https://help.monsterasp.net/websites/configuration/always-running
- https://help.monsterasp.net/websites/configuration/scheduled-tasks

## Current status

The user has an existing MonsterASP.NET onboarding email and a website-creation confirmation, and scoped WebDeploy credentials were supplied through GitHub Actions secrets. The non-mutating GitHub-to-Monster connection has been confirmed, but **no management-panel API connector or hosted runtime configuration access** is available in this workspace. An account-confirmation email is not evidence of a running app. Do not extract/share publish passwords from email or source-control them.

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
