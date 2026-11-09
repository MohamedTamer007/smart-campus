# Backend progress

## Public testing deployment continuation

The final addendum authorizes a safe feature-branch push, review PR and initial free testing deployment. The user subsequently clarified that no custom domain or Oracle account exists; use a free HTTPS hostname. Hosting, sender authorization and real service credentials remain pending.

Implemented since the local handover: Resend HTTPS mail transport with stable outbox retry keys and sanitized failure codes; explicit hosted configuration validation; certificate encryption for persisted Data Protection keys; SMTP timeout and development-only sender fallback; pinned-action Linux CI for PostgreSQL 17, three browser engines, dependency/history scans and Docker build; worker container recipe.

Observed: 31 targeted hosting/mail tests passed, followed by 67 full backend tests with real local PostgreSQL and no failures/skips. The final SMTP sender/timeout change was included in another successful 67-case regression. Solution build passed with zero warnings/errors; frontend typecheck/lint passed. Five backup safety tests passed. Encrypted database backup/isolated restore scripts and a CI restore rehearsal were added; real restore remains unverified. Discovery found 36 static/six integrated cases across Chromium/Firefox/WebKit. CI YAML parsed locally; remote workflow/container/model execution is not yet verified. Local Gitleaks release download timed out, so its full scan has not run; no scan success is claimed.

See TEST_REPORT.md and DECISIONS.md for current evidence and setup clarifications. The original local-handover record below is historical.

Base: `main` at `da7af55993e2b6dc8d41f46bd621bc10af5b97d5`. Local branch: `codex/backend-integration`. No push, hosted migration, deployment, bucket-policy change or paid inference performed.

## Current continuation evidence — 2026-11-09

The earlier local milestone below is historical. Current source adds controlled-release authorization, atomic inference budgets, administrator operational summaries, explicit per-content-version AI input review, Cairo DST-safe statistics, media-backup tooling, VM packaging, and expanded CI verification. The current public-deployment constraints and account steps are in [HOSTING_SETUP.md](HOSTING_SETUP.md).

- Full local backend: **90 passed, zero failed/skipped**, `evidence/backend-release-controls.trx`. Includes PostgreSQL-backed release restrictions, quota concurrency, reviewed AI privacy/stale-result guards, and statistics date/department cases. All seven migrations applied to isolated local test databases; latest additions also applied to the development database. Hosted schema remains untouched.
- Frontend: **47 passed**, typecheck, lint and build passed. Generated Playwright reports were added to lint exclusions after a local browser attempt exposed accidental scanning of vendor report assets.
- Worker: **6 offline tests passed** with site packages disabled. Real model inference is prepared in a separate CI Docker job and remains unverified until that job executes successfully.
- Backup guards: **7 passed, 1 skipped on Windows**; POSIX credential-permission coverage runs on Linux. Real hosted media/OCI copies, scheduling, and full recovery remain blocked.
- First GitHub CI run `42674065915`: Gitleaks full-history scan passed; 67 backend cases passed with PostgreSQL 17; actual age-encrypted dump and isolated restore/report-count comparison passed; frontend 43 checks passed. Its worker boundary failed due to an eager optional import, fixed in `97c1a22`.
- Second PR run `42674620543`: backend, backup, frontend, worker, advisory/history checks passed; Chromium had nine passing static cases and three failed account cases. Firefox launch attempts stalled until the overall job was cancelled. Static preview inherited an absent API proxy and Chromium's executable override was incorrectly shared across engines. Both configurations were corrected locally; their CI rerun is pending.
- Local native browser attempts remain blocked by missing expected Chromium build / cached executable `spawn UNKNOWN`; no local native browser pass is claimed. The earlier in-app maintenance journey remains the only completed manual browser evidence.
- Read-only hosted audit: private application schema absent, legacy AI trial count **38**, buckets private. The existing authenticated bucket-wide read policy would bypass matching for application uploads. A guarded prefix restriction and isolated proof are prepared; neither hosted permission changes nor migrations have been applied.
- Draft PR: https://github.com/MohamedTamer007/smart-campus/pull/1. Feature branch only; main unchanged. The new batch must pass its own CI before deployment.

No public URL, real verification delivery, real external inference, approved calibration, load target, or complete recovery is claimed. No paid service was enabled.

## Implemented

- .NET 10 domain/infrastructure/controller API with EF migrations in private `smart_campus` schema.
- Identity email/password flows, revocable cookie tickets, CSRF, role/department authorization, local bootstrap command.
- Maintenance, private media, custody, recovery/reservation transactions and bilateral closure.
- Notifications, administrative grants and real server-side statistics.
- OpenRouter/NVIDIA adapters, durable jobs and encrypted mail outbox, loopback SigLIP worker and pgvector retrieval.
- React service adapters, session restoration and integrated workflow controls.

## Observed checks so far

- .NET build: zero warnings/errors before subsequent review changes; final checks recorded below when rerun.
- PostgreSQL 16/pgvector 0.6 installed in existing WSL Ubuntu; independent `_dev` and `_test` databases created. Hosted target is PostgreSQL 17; hosted compatibility remains unverified.
- Backend first full successful run: 24 passed, zero failed/skipped, including real PostgreSQL concurrency tests.
- Frontend: 35 tests passed; typecheck, lint and build passed before subsequent review changes.
- Worker: six offline HTTP/input tests passed with an explicitly fake model boundary. These do not verify actual inference.
- npm audit after patching dependencies: zero vulnerabilities.

## External blockers

- Hosted runtime database/storage credentials, SMTP configuration, and provider keys have not been supplied to this application.
- Verified production locations/departments and maintenance taxonomy remain team inputs.
- General search is intentionally unavailable without reviewed calibration approval and a threshold.
- Actual SigLIP inference was attempted but the model download failed because C: ran out of disk space. The SDK archive created for this task was removed to recover space. Complete weights require additional disk space; no unrelated files were deleted.
- No hosted schema or historical AI trial records were changed. Historical trial measurements are not application acceptance evidence.

## Final verification

Final local verification completed:
- 36 backend tests passed, zero failed/skipped, including fresh migrations, real PostgreSQL/pgvector retrieval with a fake encoder, concurrency, expired tokens, session/grant revocation and notifications.
- 43 frontend tests passed; typecheck, lint and production frontend build passed.
- Six worker offline boundary tests passed. Real weights/inference remain blocked; worker now requires 4 GiB disk headroom and uses updated dependency pins and verified pooled-feature APIs.
- Final Release API build: zero warnings/errors. Local publish succeeded and excluded local configuration/demo credentials.
- Native Playwright: 12 launch failures due to cached browser binaries. Two integrated specs discovered, not executed. In-app browser verified login, location dropdowns, submission/tracking, logout, staff review/assignment, repair and resolution; screenshot saved.
- WSL initially failed during disk exhaustion/restart but subsequently recovered. Final migrations and database regressions passed after recovery. API database readiness returned ready.
- npm and NuGet advisory scans clear. Declared worker direct pins show no PyPI advisories; full worker runtime/transitive verification remains blocked.
- Generated OpenAPI has 42 paths and 34 schemas; principal DTO schema references verified. Idempotent SQL generated.
- Security, code, test and documentation review recorded in SECURITY_REVIEW.md. No push, hosted migration, production deployment or paid inference performed.

Acceptance gates and remaining unimplemented checks are in ACCEPTANCE.md. This is not certified deployment-ready.

CI update: run 42685171210 passed full-history scan, PostgreSQL 17 backend tests and isolated storage-policy proof. Actual encrypted dump/restore completed, but expanded all-row/constraint comparison failed. Constraint ordering was made deterministic by table plus constraint name (names can repeat across tables), and fixed validation reasons are now reported without row/credential data. Rerun required. Real pinned model inference already passed in run 42683987444 (768 dimensions). Manual updated administrator sign-in/live operational counts verified; screenshot admin-operations.png.

The restore comparison failure was reproduced locally and traced to PostgreSQL deparser cast normalization, not lost rows. A narrowly bounded canonicalizer plus four regression cases now passes the real local restored-copy comparison (26 tables, 42 rows, constraints/indexes/history). CI must rerun the revised verifier. No source/live records were overwritten.
