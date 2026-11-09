# Backend progress

## Public testing deployment continuation

The final addendum authorizes a safe feature-branch push, review PR and initial free testing deployment. The user subsequently clarified that no custom domain or Oracle account exists; use a free HTTPS hostname. Hosting, sender authorization and real service credentials remain pending.

Implemented since the local handover: Resend HTTPS mail transport with stable outbox retry keys and sanitized failure codes; explicit hosted configuration validation; certificate encryption for persisted Data Protection keys; SMTP timeout and development-only sender fallback; pinned-action Linux CI for PostgreSQL 17, three browser engines, dependency/history scans and Docker build; worker container recipe.

Observed: 31 targeted hosting/mail tests passed, followed by 67 full backend tests with real local PostgreSQL and no failures/skips. The final SMTP sender/timeout change was included in another successful 67-case regression. Solution build passed with zero warnings/errors; frontend typecheck/lint passed. Five backup safety tests passed. Encrypted database backup/isolated restore scripts and a CI restore rehearsal were added; real restore remains unverified. Discovery found 36 static/six integrated cases across Chromium/Firefox/WebKit. CI YAML parsed locally; remote workflow/container/model execution is not yet verified. Local Gitleaks release download timed out, so its full scan has not run; no scan success is claimed.

See TEST_REPORT.md and DECISIONS.md for current evidence and setup clarifications. The original local-handover record below is historical.

Base: `main` at `da7af55993e2b6dc8d41f46bd621bc10af5b97d5`. Local branch: `codex/backend-integration`. No push, hosted migration, deployment, bucket-policy change or paid inference performed.

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
