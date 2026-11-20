# Deployment and release record

## Current verified deployment — 18 November 2026

The application is hosted at [smartcampus.tryasp.net](https://smartcampus.tryasp.net/). [MonsterASP deployment run 44348910144](https://github.com/MohamedTamer007/smart-campus/actions/runs/44348910144) was manually dispatched against main commit b427e37b958d46d9f9348d63791a5e742cf8068f. The Windows build, pre-deployment HTTPS liveness check, WebDeploy dry-run, compiled application upload and post-deployment /health/live probe all succeeded.

This confirms a successful code upload and API liveness at the time of the check. It does not prove complete worker continuity, real email delivery, external AI availability, hosted private-storage authorization, human acceptance or production suitability. The public MonsterASP Free plan is synthetic-data evaluation only; see [the provider's Terms clause 3.2](https://www.monsterasp.net/Terms/).

## Source-commit verification on future Windows deployments

A release-readiness improvement adds a public, non-secret `/build-info.json` marker containing only the source Git commit SHA. `scripts/publish.ps1` writes it into the compiled `wwwroot` bundle, and `scripts/deploy-monsterasp.ps1` requires its presence, completes the guarded WebDeploy upload, tests `/health/live`, then requests the published marker with a cache-busting query. The deployment workflow must fail if the live SHA does not match the uploaded package, instead of mistaking an older healthy process for the new release.

**Evidence boundary:** this check establishes that the target host serves the expected release marker, in addition to API liveness. It does not independently prove all static/server code paths, secret configuration, role authorization, email, storage policy, human acceptance or AI accuracy. The updated scripts were merged in PR #21 on 20 November 2026 as `eb3c62c2c743ce792a53f311ce97c3751d422437`; the marker check becomes evidence for a specific release only after the corresponding deployment run demonstrably succeeds. A successful PR package build by itself does not establish live deployment.

The remaining sections below contain the original deployment preparation and database rollout notes. Statements in those historical notes that no deployment had occurred refer to the 10 November baseline and are superseded by the 18 November run above.

# Deployment preparation

The private application schema has now been deployed to the existing Supabase PostgreSQL project and verified, but **no API/worker/frontend production deployment or cutover** was performed. Final Release compilation and local publish succeeded; final evidence is in BACKEND_PROGRESS.md. Container execution and hosted integration remain unverified.

Current free-provider comparison and account setup are in [HOSTING_SETUP.md](HOSTING_SETUP.md). Linux VM packaging is prepared in `deployment/compose.yml`: Caddy serves the single HTTPS origin, API and worker bind loopback in the host network, private environment files are separate, and model cache/key storage persist outside images. This packaging has not yet passed CI/runtime verification. The full application's free platform-hostname requirement remains unresolved; no public URL is supplied as a completed deployment.

## Application shape

Serve React and `/api/v1` from one HTTPS origin. `scripts/publish.ps1` builds React, publishes the Release API and copies assets into published wwwroot. `Dockerfile` provides an alternative Node/.NET multi-stage build with a non-root runtime user. The Python worker runs on the API host's loopback interface; if using containers, place both processes in the same network namespace. Do not expose its port publicly. Weights, key rings and uploads are outside the source image.

Set `ASPNETCORE_ENVIRONMENT=Production`, `PublicOrigin=https://YOUR_APPROVED_HOST`, and `AllowedHosts=YOUR_APPROVED_HOST`. Terminate TLS at the approved proxy or configure Kestrel certificates. Behind a proxy, set `ReverseProxy__KnownProxy` to its exact trusted IP; only then does the application consume forwarded scheme/client-IP headers. Do not trust arbitrary forwarded headers. Use persistent, access-restricted `DataProtection__Directory` storage; back up the key ring. Windows keys use the service account's DPAPI identity, which must be preserved when restoring them.

For containers, restrict host publication to the local TLS proxy, such as loopback port 8080. Add no public listeners without approval. The image is prepared, not validated by Docker in this environment.

## Database rollout

1. Inspect the intended target and take an appropriate backup. Preserve all legacy public tables and trial records.
2. Enable/verify pgvector and its schema. Use a migration connection with DDL rights and `Search Path=smart_campus,extensions,public`. Hosted connections must verify TLS certificates, for example Npgsql `SSL Mode=VerifyFull`; use the actual direct/session-pooler connection details supplied by Supabase.
3. **Completed 2026-11-10 for the existing target:** reviewed `docs/database.sql`, rehearsed all eight EF migrations in a hosted PostgreSQL transaction followed by `ROLLBACK` (26 temporary tables), verified that no changes persisted, then applied the script to the isolated `smart_campus` schema. Follow-up checks confirmed 26 application tables, all eight EF migration records, and no change to the 38 legacy `public.ai_trial_records`; `anon` and `authenticated` cannot `USAGE` the new schema. A separate staging environment and full hosted API/worker integration still require validation.
4. Provision a distinct runtime role with USAGE on smart_campus and the vector extension schema, and SELECT/INSERT/UPDATE/DELETE on application tables. Do not grant DDL, ownership, superuser or legacy-table access. Do not expose smart_campus through the Data API or grant anon/authenticated access.
5. Switch the application to runtime credentials. Migration history remains owned by the migration role; startup never applies migrations.

Use forward corrective migrations for rollback. Down migrations delete application structures and must not be run against shared data without approval. No automatic legacy Auth conversion exists; the new Identity store is deliberately separate.

## External configuration

- Configure private storage URL/key only on the API. Existing maintenance-images/lost-found-images buckets must remain private. New objects use a smart-campus prefix and are served through an authorizing proxy. Audit old bucket policies against this prefix before real use; no policy changes were performed here.
- Configure SMTP host, port 587 with STARTTLS, sender and credentials. Confirm real verification/reset delivery and university-domain deliverability. Local mail-sink success is not SMTP evidence.
- Configure reviewed maintenance taxonomy and authorized non-sensitive provider usage. Free model availability and quotas are not guaranteed. Image forwarding is independently disabled by default. No paid fallback exists.
- Install worker dependencies and the pinned weights with enough disk/RAM. Set a random worker key of at least 32 characters on both processes. Verify actual text/image embeddings and database retrieval before enabling user search.
- Supply verified locations/departments and staff grants. No production campus directory or staff account is fabricated.
- Keep Search:CalibrationApproval empty until reviewed positive/no-match fixtures establish a release filter. Thresholds/weights are configuration decisions, not ownership probabilities.

## Readiness and operations

`/health/live` is process liveness. `/health/ready` checks database connectivity only; it does not certify schema, SMTP, storage, worker, calibration or providers. Monitor job state/attempts and provider failure codes in the private database, and exercise actual journeys. Do not expose an operational SQL console publicly. Logs omit provider payloads and secrets; protect database backups, key rings and application logs.

Before cutover, run the acceptance matrix, including concurrency and security tests, full browser journeys, authorized media access and real external-service checks. Resolve disk/WSL/browser blockers and rerun latest migrations first. Require explicit user authorization before production deployment, shared-branch publication, permission changes, destructive migrations or paid resources.
