# Smart Campus — SnapDeploy Free deployment candidate (2026-11-10)

This is an unlaunched **zero-card, zero-subscription** candidate for the **real**
ASP.NET Core + React application, not the static preview.

## Selection evidence

- Provider free docs: https://snapdeploy.dev/docs/free-tier
- GitHub + Docker deployment docs: https://snapdeploy.dev/docs/deployment
- Private GitHub scope caveat: https://snapdeploy.dev/docs/github
- Runtime secret injection: https://snapdeploy.dev/docs/environment-variables

The free tier advertises **no card**, a **512 MB / 0.25 vCPU container**,
**100 container-hours per month**, automatic sleep after about 15 minutes idle,
browser wake after about 60 seconds, a hosted HTTPS subdomain, up to four
containers and 10 deployments per day (five per rolling 12 hours).
When the free hours run out, it cannot wake until the next month. It provides
no persistent disk; keep all persistent records on the existing Supabase
database and private object storage. Third-party API/cron calls do not wake a
sleeping free container. Do not enable Always-On, paid databases, or sprint
packs.

Builds from GitHub use **ARM64**, and source is cloned from the linked branch.
Our standard root Dockerfile uses official Node 24 / .NET 10 multi-stage
images and now explicitly declares **EXPOSE 8080** for platform port detection.
The application recognizes `Hosting__Platform=snapdeploy` and relies on
the platform's TLS-terminating public edge rather than generating internal
redirect loops. Deploying the app on ARM64, testing native SkiaSharp libraries,
and its cold-start/RAM usage are **not yet verified** on this provider.

## Deploy only after runtime prerequisites

1. Sign up without a payment card: https://snapdeploy.dev/
2. Choose GitHub repo **MohamedTamer007/smart-campus** (private), branch
   **codex/backend-integration**, root **.**, Dockerfile **./Dockerfile**,
   container **Small / Free**. Connecting a private repo through their GitHub
   OAuth requests broad `repo` scope, which can include other private repos.
   The owner must review/accept this authorization; do not assume repo-only
   access. Never merge the draft PR into main to grant hosting access.
3. Prevent auto-redeploy during runtime setup if the interface supports it.
   Monitor the monthly free-hour quota and build/deploy quota.
4. Configure **non-secret** runtime environment:
   `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`,
   `Hosting__Platform=snapdeploy`, `Storage__Local=false`,
   `Storage__Url=https://mninpcrhqsaafoshjqpn.supabase.co`,
   `Email__Provider=resend`, `Email__LocalSink=false`,
   `DataProtection__Store=database`, `Release__Mode=controlled`,
   `AI__AllowNonSensitiveInference=false`, `AI__AllowNonSensitiveImages=false`.
5. Obtain the **actual** HTTPS hostname before setting `PublicOrigin` and
   `AllowedHosts`; never guess it. Set these runtime secrets **in SnapDeploy's
   secret environment screen**, not in source or GitHub artifacts:
   `ConnectionStrings__Campus` (dedicated least-privilege Supabase role and
   `SslMode=VerifyFull`), `Storage__Key` (scoped private media key),
   `Email__From` and `Email__ResendKey` (authorized verified sender),
   `Release__AllowedEmails__0` (genuinely authorized university test email),
   `DataProtection__CertificateBase64` (private PFX) and
   `DataProtection__CertificatePassword` (private secret).
6. Apply the reviewed non-destructive ninth migration
   `20261110225500_RenderPersistentKeys` to the isolated
   `smart_campus` schema using a **separate migration role**; no app
   auto-migration. This stores certificate-encrypted key ring XML in
   PostgreSQL rather than the ephemeral filesystem. Verify encrypted
   records survive a restart.
7. Review the hosted Supabase media bucket SELECT policy and only then
   allow any actual student images. Test email verification and recovery
   end-to-end; the generic trial `onboarding@resend.dev` sender will not
   deliver to all university students.
8. Only after the environment is complete should the real app be
   launched and verified at `/health/live` and `/health/ready`, followed
   by real registration, auth, maintenance and lost/found acceptance.
   Those endpoints do not prove the entire email/AI/media workflow.
   The existing Python SigLIP retrieval worker is **not** provided by
   the basic container; image-search API migration and calibrated
   embeddings remain separate development work.

**Status:** No SnapDeploy account or service connection has been established
in this ChatGPT session; the user must authorize its GitHub OAuth if selected.
No paid resources or production services created through this preparation.
