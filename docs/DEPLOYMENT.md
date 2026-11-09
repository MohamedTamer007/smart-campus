# Deployment preparation

No deployment or production cutover was performed. Final Release compilation and local publish succeeded; final evidence is in BACKEND_PROGRESS.md. Container execution and hosted integration remain unverified.

Current free-provider comparison and account setup are in [HOSTING_SETUP.md](HOSTING_SETUP.md). Linux VM packaging is prepared in `deployment/compose.yml`: Caddy serves the single HTTPS origin, API and worker bind loopback in the host network, private environment files are separate, and model cache/key storage persist outside images. This packaging has not yet passed CI/runtime verification. The full application's free platform-hostname requirement remains unresolved; no public URL is supplied as a completed deployment.

## Application shape

Serve React and `/api/v1` from one HTTPS origin. `scripts/publish.ps1` builds React, publishes the Release API and copies assets into published wwwroot. `Dockerfile` provides an alternative Node/.NET multi-stage build with a non-root runtime user. The Python worker runs on the API host's loopback interface; if using containers, place both processes in the same network namespace. Do not expose its port publicly. Weights, key rings and uploads are outside the source image.

Set `ASPNETCORE_ENVIRONMENT=Production`, `PublicOrigin=https://YOUR_APPROVED_HOST`, and `AllowedHosts=YOUR_APPROVED_HOST`. Terminate TLS at the approved proxy or configure Kestrel certificates. Behind a proxy, set `ReverseProxy__KnownProxy` to its exact trusted IP; only then does the application consume forwarded scheme/client-IP headers. Do not trust arbitrary forwarded headers. Use persistent, access-restricted `DataProtection__Directory` storage; back up the key ring. Windows keys use the service account's DPAPI identity, which must be preserved when restoring them.

For containers, restrict host publication to the local TLS proxy, such as loopback port 8080. Add no public listeners without approval. The image is prepared, not validated by Docker in this environment.

## Database rollout

1. Inspect the intended target and take an appropriate backup. Preserve all legacy public tables and trial records.
2. Enable/verify pgvector and its schema. Use a migration connection with DDL rights and `Search Path=smart_campus,extensions,public`. Hosted connections must verify TLS certificates, for example Npgsql `SSL Mode=VerifyFull`; use the actual direct/session-pooler connection details supplied by Supabase.
3. Generate/review SQL with the pinned EF tool, or run the explicit `--migrate` command using migration credentials. Apply to an isolated staging database first. The newest actor-integrity migration passed local database rechecks after WSL recovery. Hosted staging validation is still required.
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
