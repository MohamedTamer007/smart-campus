## 12 November 2026 — Admin AI diagnostics use the configured application budget

- Removed the extra one-per-day OpenRouter diagnostic cap and fixed five-per-day NVIDIA cap. Each admin-only diagnostic may now use up to the provider's configured daily application budget; OpenRouter's diagnostic requires three requests per attempt, so the shared request budget and per-minute limit stop it before it can exceed the configured provider allowance.
- The endpoints still require an authenticated administrator and send only fixed fictional prompts plus a generated 32×32 synthetic image. Provider keys remain server-side, and diagnostic payloads cannot include caller-supplied content. This does not change provider-side free-plan quotas or guarantee availability.
- Updated the admin status text and integration coverage to permit a second OpenRouter diagnostic while preserving the shared provider budget tests. No hosted environment variables or counters were changed/reset.
- Live verification on commit `94e36a1`: the OpenRouter synthetic diagnostic passed twice, validating image analysis and text/image embeddings with the configured hosted model. NVIDIA's fictional classification and simple connectivity probes both timed out at the application timeout; NVIDIA text inference is not confirmed working. Public recovery search remains disabled pending privacy approval and calibration.
- PostgreSQL CI exposed three test failures. The operational-summary assertion now matches the default fictional taxonomy, the quota integration fixture clears provider usage in its isolated test database, and bulk maintenance lists omit the AI explanation while staff report details retain it.

## 12 November 2026 — Traceable AI demo failures without additional external inference

- The hosted admin fictional-classification route returned an uncaught `internal` (500). Hosted PostgreSQL usage counters had incremented, so the endpoint passed initial quota reservation but the true provider outcome was not established. No provider response or underlying exception details were available.
- `AiProviders.Assess` now maps TLS/network (`HttpRequestException`, read `IOException`) and non-client-cancelled timeout failures to sanitized `ai_connection_failed` and `ai_timeout` rule codes; it distinguishes key rejection (401), access rejection (403), invalid parameters (400/422) and preserves existing provider and parser failures. The UI explains each code without rendering provider bodies, URLs, or credentials.
- NVIDIA inference requests get a bounded 90-second hosted timeout, up from 45 seconds, to permit slower model cold starts. Existing staff privacy gates, inference budget, daily diagnostic cap (5), credentials and deployment strategy remain unchanged. The current day's usage cap is **not** reset or bypassed.
- Added purely simulated HTTP handler tests for SSL/transport failures, timeout and rejection responses. A subsequent successful build/deploy does not prove NVIDIA itself works; owner must inspect the next safe error code after the UTC quota reset.

## 12 November 2026 — Admin fictional AI diagnostic remains bounded

- Increased the *admin-only, fixed-fictional-input* diagnostic cap from 3 to 5 attempts per UTC day. This permits two additional troubleshooting attempts after the three already recorded today; preserves all usage rows and the provider's distinct 50/day app quota; makes no modifications to real student-report transfer or AI privacy switches.
- The same cap applies to both NVIDIA connectivity and the real-model fictional-classification demo. The UI and integration-test cap assertions now match 5.
- Increasing an application's diagnostic cap does not prove provider connectivity or fix provider failures: require a live admin test and inspect the safe error response.

## 12 November 2026 — Real AI classifications on fictional data, no student-data release

- Added admin-only `POST /api/v1/admin/operations/ai-demo` with a fixed fictional maintenance report, no request-body intake, CSRF/admin checks, and server-owned NVIDIA key. It calls the *actual* AiProviders classification pathway, validates the response labels, and displays the returned type/priority/risk/explanation. Both diagnostics share the preexisting three-per-UTC-day provider attempt cap.
- Updated NVIDIA GPT-OSS classification to use supported `reasoning_effort=low` request parameters instead of assuming unsupported JSON mode, while keeping OpenRouter's structured output mode unchanged. Parsers continue to fail closed on unexpected labels and malformed output.
- Real student-report inference remains disabled (`AI:AllowNonSensitiveInference=false` and `AI:AllowNonSensitiveImages=false`). The diagnostic has no access to user report content and makes no asynchronous provider requests. The free MonsterASP host cannot be used for production processing of student personal data under the provider's terms.
- A code deployment is not proof of real model output: an authenticated administrator must run the fictional demo after the UTC attempt cap resets and confirm the result.

## 12 November 2026 — Synthetic NVIDIA diagnostic: isolate endpoint incompatibilities

- Admin diagnostic now sends a fixed fictional prompt with the basic Chat Completions request shape documented for `openai/gpt-oss-20b`: `reasoning_effort=low`, `max_tokens=1024`, `stream=false`, and no `response_format` constraint. It verifies a completed nonempty provider reply without returning or logging model text.
- Live maintenance classification stays disabled; no user reports or photos leave the app in this path. NVIDIA HTTP status maps to sanitized fixed error codes (key rejected, access denied, rate limit, incompatible request, provider unavailable, connection failed, timeout, malformed response), displayed in Arabic and English. Keys, request content and provider bodies are not exposed.
- The synthetic attempt cap remains three per UTC day, enforced in PostgreSQL; actual provider quota remains charged. Two attempts were recorded in the hosted database before this change, so only one remained on the same UTC day. No quota was reset.
- Prior failures were generic and did not establish whether credentials or request parameters caused the problem. Passing this new button proves live **connectivity**, not the stricter maintenance classification integration.

## 12 November 2026 — Admin-only synthetic NVIDIA connectivity diagnostic

- Added a **separate admin-only** NVIDIA test route behind `AI:AdminSyntheticDiagnosticEnabled` in versioned, non-secret defaults. It sends only one hard-coded fictional plumbing description and in-memory fictional allowed labels. No actual maintenance report, photo, student identity or private submission reaches NVIDIA.
- The diagnostic is independent of `AI:AllowNonSensitiveInference` and live institutional taxonomy, which remain unchanged. It uses the existing server-secret NVIDIA API key (never committed), existing provider quota, existing antiforgery/auth/admin checks, and a server-side cap of three attempts per UTC day. It reports only validated status, provider, model and no model text.
- The admin UI displays a distinct test button when the diagnostic flag, NVIDIA key and positive quota are present. Existing real maintenance AI stays disabled unless separately approved. No AI request was triggered by the deployment itself. An authorized admin must click the diagnostic and check its result to establish real provider connectivity.
- GitHub CI and Windows IIS deployment results must be checked on the new commit; a passing build alone is not a live NVIDIA response.

## 12 November 2026 — Non-secret configuration included in automatic WebDeploy

- Added `backend/SmartCampus.Api/smartcampus.defaults.json`, read after core appsettings but before local development config and host environment overrides. Non-secret defaults for safe feature gates, storage host URL and rate limits now travel with routine GitHub deployments.
- The API project marks that JSON file for publication; the existing WebDeploy script explicitly verifies its presence and refuses known secret-key names. This avoids overwriting `appsettings.json`, `appsettings.Production.json`, `web.config`, uploaded objects, hosted secret variables or key-ring files.
- Existing secrets and environment-dependent values remain host-owned and must be provisioned securely. GitHub WebDeploy secrets are deployment credentials, not runtime API credentials. No automated mutation of the MonsterASP environment-variable dashboard was attempted.
- CI must verify the compiled publish package and live pipeline before any claim of successful update.

## 12 November 2026 — Automatic MonsterASP.NET release pipeline (controlled branch)

- Owner requested automatic site publishing after changes, and supplied the four WebDeploy repository secrets. Added `scripts/deploy-monsterasp.ps1` and `.github/workflows/monsterasp-auto-deploy.yml` on `codex/release-readiness-20261111`; default `main` and the existing Cloudflare Pages integration were not changed.
- The workflow tests frontend and backend, builds the combined Windows .NET 10 / React publish output, does a credentials-safe MSDeploy dry-run, and then publishes to the existing MonsterASP site. It checks `https://smartcampus.tryasp.net/health/live` both before and after deployment.
- To protect the existing host, WebDeploy uses `DoNotDeleteRule` and skip rules for `web.config`, `appsettings*.json`, persistent storage, uploads, `App_Data`, and data-protection directories. It does not run database migrations, modify provider secrets, or change account release grants. AppOffline is used only during the actual sync.
- GitHub Actions [run 43223686847](https://github.com/MohamedTamer007/smart-campus/actions/runs/43223686847) on commit `088c6de51f84fce1ba466ab2007baa730f455282` completed **successfully**. GitHub reports frontend tests, backend tests, Windows publish, and WebDeploy steps as successful. This is evidence of automated code publishing and API liveness only; it does not establish mail, AI provider, complete worker, student-data, or university release acceptance.
- A follow-up workflow edit limits push-triggered deployment to relevant application/build/deployment paths. Documentation-only changes do not redeploy the server.

## 12 November 2026 — Verified standard-user synthetic AI check (feature branch only)

- Added a separate verified-user `GET/POST /api/v1/ai/smoke` behind the existing admin smoke flag **and** `AI:SyntheticUserSmokeTestsEnabled` (new default false). Requires `Release:Mode=controlled` and `Release:AllowedEmails`, so public release modes cannot enable it. The endpoint tests only fixed fictional maintenance text, never uploaded files/reports or arbitrary user prompts.
- Server-side atomic Postgres counters cap calls at **one attempt per user per UTC day and three attempts per test deployment per UTC day**, in addition to provider budgets. No migrations or tables were added: counters reuse `InferenceUsage`; a failed external provider attempt still consumes the test slot.
- Student maintenance list displays the button only when the server reports the test enabled; does not reveal any key, data, or AI suggestion. Added backend tests for disabled state, anonymous/CSRF rejection, fixed text, success and repeated attempt. The prior admin smoke route remains restricted to administrators.
- CI run 43124171623 reported a failure in Playwright browser launch timeout; its backend suite passed **157/157**. This change needs fresh CI proof. No production deployment, role grants, live secrets, real AI requests, migrations or student-data transfer occurred.

## 12 November 2026 — Opt-in admin synthetic AI smoke check (feature branch only)

- Added a disabled-by-default `AI:SyntheticSmokeTestsEnabled` switch and admin + CSRF protected `POST /api/v1/admin/operations/ai-smoke`. On explicit admin request in an isolated test host, it submits only a hard-coded fictional maintenance description through the existing, quota-limited AI provider adapter. The minimal response contains status/provider/model, never credentials or model text. The UI describes consumption of one provider request and shows a button only when the smoke switch and text configuration are enabled.
- Added backend isolated-database tests for the default-disabled state, staff denial, and one successful synthetic provider call using a mock HTTP boundary. These tests do NOT contact NVIDIA. Added test-only setup instructions; no configuration switch was toggled on the deployed site and no real provider call was performed.
- Earlier current-head CI run 43122270924 finished successfully (Backend/Frontend/Browser/Packaging); this new change needs a fresh CI run. Real AI provider acceptance remains unresolved.

## 12 November 2026 — Approved maintenance image normalization (feature branch only)

- Corrected provider payload generation for reviewed PNG/WebP maintenance photos: previously the raw bytes were labeled `data:image/jpeg` regardless of actual format. Approved images now undergo format/size/dimension checks, decode to pixels and convert to JPEG before the outbound request; metadata/EXIF is not transferred.
- Added regression tests using an actual synthetic PNG and decoding the outgoing data URL as JPEG; invalid approved image input now fails before a provider request. Previous image-sharing approval gates remain unchanged and default to false.
- No real student photos, secrets, database records or provider calls were used. CI on the new commit is needed to establish test/build status.

## 12 November 2026 — Admin AI readiness visibility (feature branch only)

- Added a **read-only admin-only** AI readiness summary to `GET /api/v1/admin/operations`. It reports provider selection, text/image gate status, taxonomy presence, key-presence booleans and whether app-configured budgets are positive. It does not include any credential values, sensitive records, inference prompts or provider requests.
- Displayed the flags in the admin operations screen, including explicit language that a configured route is **not** a proven successful model response; frontend tolerates an older backend that does not yet return `ai`.
- Updated isolated database-backed admin API assertions for the new contract. Original guest/student/staff access-denial tests remain in place.
- No direct provider requests, hosted secret reads, database writes, production deployment, release-flag changes or switch activations were performed. At authoring time the previous [CI run 43121148313](https://github.com/MohamedTamer007/smart-campus/actions/runs/43121148313) was still running. This workspace has no .NET SDK; GitHub CI is required for backend build and tests.

## 12 November 2026 — Hosted AI image-review routing fix (feature branch only)

- Inspected draft PR #2 on branch `codex/release-readiness-20261111`, the AI routing and maintenance approval code, and the user's design-atlas ZIP. The archive contains diagrams/submission materials, not deployable backend source or a reviewed institutional AI taxonomy.
- Fixed a mismatch: `AiProviders.SelectedProvider(auto, hasApprovedImage:true)` routes to OpenRouter, but `MaintenanceService` had required the literal `AI:Provider=openrouter` to expose/accept a reviewed image. Both now use `AiProviders.CanReviewImage`, which requires a configured provider, explicit non-sensitive image approval and an image-capable route.
- Added a fail-closed provider guard so direct calls to `Assess` with an image reject unapproved images before reserving budget or contacting an external provider. Added isolated mocked-provider unit cases for auto, openrouter, NVIDIA, disabled images, disabled general inference and missing NVIDIA daily budget.
- Added a synthetic-only smoke-test procedure in `docs/HOSTED_AI_MODELS_2026-11-11.md`. Example taxonomies are *not* university-approved classifications, and no provider/image privacy gate was flipped on the live host.
- Evidence: GitHub commits `a7b5b1a`, `9d382385`, `62a1dbe` on the draft branch; GitHub CI run [43120917022](https://github.com/MohamedTamer007/smart-campus/actions/runs/43120917022) was **in progress at the time of this note**. This local workspace has no .NET SDK; no local `dotnet build`/`dotnet test` was executed, and no real NVIDIA/OpenRouter request was made.
- Live hosting was not mutated: no API key was read, no production data was transferred, no database migration, web deployment, PR merge, calibration approval or paid resource was performed. Owner reported that `/health/live` and `/health/ready` returned successful JSON, which proves only basic API/DB readiness, not external AI acceptance.

## 11 November 2026 — SnapDeploy live checks and release gates

### Follow-up audit — 11 November 2026

After the owner asked us to enter the provider dashboards, an existing signed-in SnapDeploy browser session was available. The dashboard shows container `smart-campus-arm-b7d59` as **RUNNING**, configured to sleep after 15 minutes idle; the free deployment allowance is currently **5/5 used**, resetting in about 11 hours. Its public HTTPS origin is `https://smart-campus-arm-b7d59.containers.snapdeploy.app/`. These dashboard values do not override the failed public 503 probes above. Creating another deployment now would exceed the visible free window, so no deploy/Always-On purchase was started. SnapDeploy logs could not be opened reliably because the browser automation lost its UI element state; no log contents were observed.

The application-code head at the start of this audit was `cb658c74f82d39e830a6269d90817dd0ae338a6a`. CI run [42929593009](https://github.com/MohamedTamer007/smart-campus/actions/runs/42929593009), the Windows package run [42929592842](https://github.com/MohamedTamer007/smart-campus/actions/runs/42929592842), and real SigLIP inference run [42929592876](https://github.com/MohamedTamer007/smart-campus/actions/runs/42929592876) completed successfully on that source. These results do not prove that the public SnapDeploy container is healthy.

The first public probes in this session returned HTTP 503 from the SnapDeploy edge for both `/health/live` and `/health/ready`. After opening the existing container URL to wake it, a follow-up `GET /health/live` returned the app's HTTP 200 `{"status":"live"}` response. `GET /health/ready` still returned HTTP 503 with SnapDeploy's “Waking up your container...” HTML, not the app's readiness response. Runtime database readiness remains unverified. No redeploy was attempted. There is no Modal account connector in this session; the SnapDeploy dashboard was inspected using the existing signed-in browser session.

Read-only Supabase inspection identified project `mninpcrhqsaafoshjqpn` (Postgres 17.11). The management migration list contains three hosted entries (`core_schema`, `policies_and_workflows`, `smart_campus_backend_ef_core_schema_20261110`), separate from the application's EF migration history. Security advisors reported an exposed `public.ai_trial_records` table with RLS enabled but no policy, a mutable search path on `public.apply_ai_suggestion`, and public/authenticated execute grants on multiple `SECURITY DEFINER` functions. Performance advisors reported nine unindexed foreign keys and RLS init-plan warnings. These include legacy public objects; no SQL, policy, account, or storage changes were made. Before remediation, compare the exact hosted grants/functions and bucket policy against repository SQL and preserve legacy AI records.

Real-account acceptance remains blocked: no Outlook/Entra delegated `Mail.Send` consent or verified message delivery, no Modal account/budget evidence or deployed worker URL, and no OpenRouter secret are available in this session. Credentials must be entered only into their provider or intended secret store. Email remains incomplete until a real recipient confirms delivery and completes the link; search remains disabled until the hosted worker and calibration pass. Keep PR #1 Draft and `main` unchanged.

The owner supplied a SnapDeploy dashboard screenshot showing the existing `smart-campus-arm-b7d59` container as RUNNING. This establishes that a container exists; it does not establish application or dependency readiness. The dashboard showed the free deployment limit exhausted for the current 12-hour window (about 11h45m remaining at screenshot time), so no deployment was started.

Observed from this session:
- `GET https://smart-campus-arm-b7d59.containers.snapdeploy.app/health/live` returned HTTP 200 with `{"status":"live"}`.
- A non-browser `GET /health/ready` returned SnapDeploy's HTTP 503 "Waking up your container..." page. This is a platform wake response, not the application's readiness response, so the runtime database connection is still unverified.
- Smart Campus CI run [42928125451](https://github.com/MohamedTamer007/smart-campus/actions/runs/42928125451) passed on branch head `a6f363c17e341681729645567ea0d0768cad0e91`. Worker inference run [42928125175](https://github.com/MohamedTamer007/smart-campus/actions/runs/42928125175) passed.
- ARM64 image build run [42927487772](https://github.com/MohamedTamer007/smart-campus/actions/runs/42927487772) passed on `4172fe281ea2a80d1d72bf58a85b132dbc0c2e9c`. It produced private artifact `smart-campus-arm64-image` (278,073,284 bytes), available until 2026-11-18. The subsequent branch-head change was limited to the public pooler TLS check workflow; the app image source did not change.
- No new image was uploaded to SnapDeploy because its free deployment quota was exhausted.

**Remaining release gates:** fetch a browser-triggered readiness result from the existing container; verify its runtime database connection without exposing credentials; configure and verify an approved mail sender/test account; run real private-media acceptance; and provide an always-available, free worker host with enough resources for the SigLIP model. No institutional directory, account set or approved sender is available, and no paid services are authorized. Hugging Face's current Spaces documentation says compute Spaces on CPU Basic require a paid plan, have non-persistent disk and sleep on inactivity ([official overview](https://huggingface.co/docs/hub/spaces-overview)); therefore it does not qualify as an always-available free worker host. Image search remains disabled pending a viable host and reviewed calibration.

These results do not certify a full production release. The PR remains Draft and `main` remains unchanged.

## 10 November 2026 — Verified static MonsterASP.NET preview (no user data)

Owner authorized continued deployment on the existing `smartcampus.tryasp.net` host.
Added a separate static Vite entry, `preview.html` and
`src/preview-main.tsx`, that shows the real product visual direction in
Arabic/English but explicitly does not support account, maintenance or custody
submissions. No backend API calls or secrets are included in the preview bundle.

The first attempt to publish under `site/preview` completed MSDeploy but
failed the public smoke test with HTTP 404
([run 42885283927](https://github.com/MohamedTamer007/smart-campus/actions/runs/42885283927)).
The corrected destination `site/wwwroot/preview` follows ASP.NET Core's
nested static assets directory and passed the live public HTTP smoke test
([run 42885734797](https://github.com/MohamedTamer007/smart-campus/actions/runs/42885734797)).
A further real publish and verification pass
([run 42886109744](https://github.com/MohamedTamer007/smart-campus/actions/runs/42886109744))
**passed**, confirming the public `/preview/` directory URL, its HTML,
compiled JS and CSS and the university logo are accessible. WebDeploy
`-whatif` ran before each write and `DoNotDeleteRule` retained unrelated
files. Confirmed live URL:
`http://smartcampus.tryasp.net/preview/`.

**Not passed/not attempted:** the full .NET runtime at public origin,
HTTPS certificate, real identity verification and recovery email, external
private media, separate Python AI inference, reliable hosted background jobs,
real university account acceptance or 100% production functionality. The
existing private `smart_campus` Postgres schema was not changed by the
preview publishing. The backend release remains gated by an explicit
manual dispatch and approved real environment configuration.
Current detailed evidence is in `docs/MONSTERASP_DEPLOYMENT.md`.
Do not promote the preview to university production or misdescribe its
static buttons as working services.

## 10 November 2026 — Frontend error handling hardening

Source review identified two UI failure paths: unexpected (non-ServiceError) failures from data loaders were rethrown in asynchronous handlers, leaving the view in loading state, and unexpected form-action failures could become unhandled without user feedback. The settings sign-out control also bypassed the shared action error handling. Updated AsyncView, useAction and sign-out to show a recoverable unavailable outcome and allow retry without assuming an operation succeeded. Added two targeted React regression tests for the failure/retry paths. New GitHub CI verification is required; this change does not certify deployment or unblock hosted database, email, university directory, or calibrated AI search.

## 10 November 2026 — Resend verification preparation

Current local backend regression: 97 passed, zero failed/skipped (`evidence/backend-resend-evidence.trx`); targeted mail safety/evidence cases: 20 passed. The eighth EF migration adds nullable provider-message/failure metadata and was applied only to the isolated development database. Windows PowerShell 5.1 and PowerShell 7 launcher preflight passed. Source, security, test and documentation review checked recipient restrictions, hidden key entry, transport acceptance evidence, encrypted payload cleanup and durable retry behavior.

CI for committed e4443a7c6200f57b1045c7c275e53a6a9723347b succeeded in run 42693231390: 92 backend cases on PostgreSQL 17, 54 frontend cases, 36 static browser cases and nine integrated cases across Chromium/Firefox/WebKit, isolated storage-policy and encrypted restore checks, and API image build. Real-worker run 42693230873 succeeded. These runs precede the current Resend evidence changes and do not establish production deployment.

Resend tools remain unavailable in this session. The owner reported launching the secure helper, but the subsequent health request was refused and no dotnet process or API listener was present. Startup diagnosis is pending the owner's window message. No real message has been submitted by this test; provider acceptance, delivery and owner verification remain BLOCKED. No verification token was consumed and no account confirmation was bypassed.
# Backend progress

## Latest checked milestone

Local backend: **92 passed, zero failed/skipped** (vidence/backend-cookie-race.trx); frontend: **54 passed**, typecheck/lint/build passed. Real pinned-model CPU inference, PostgreSQL 17 tests, complete application restore comparison, isolated storage-policy proof and media crypt roundtrip have passed CI. All 36 static browser checks passed. The latest integrated run passed five cases and exposed a session-renewal/logout race; an atomic fix and real lock-barrier regression now pass locally, with integrated/container rerun pending. No public deployment, hosted migration/permission change, real email/provider call, calibrated search release or paid resource is claimed. See HOSTING_SETUP.md for owner setup steps. Earlier milestones below retain their observed historical counts.

## Historical deployment setup milestone

The final addendum authorizes a safe feature-branch push, review PR and initial free testing deployment. The user subsequently clarified that no custom domain or Oracle account exists; use a free HTTPS hostname. Hosting, sender authorization and real service credentials remain pending.

Implemented since the local handover: Resend HTTPS mail transport with stable outbox retry keys and sanitized failure codes; explicit hosted configuration validation; certificate encryption for persisted Data Protection keys; SMTP timeout and development-only sender fallback; pinned-action Linux CI for PostgreSQL 17, three browser engines, dependency/history scans and Docker build; worker container recipe.

Observed: 31 targeted hosting/mail tests passed, followed by 67 full backend tests with real local PostgreSQL and no failures/skips. The final SMTP sender/timeout change was included in another successful 67-case regression. Solution build passed with zero warnings/errors; frontend typecheck/lint passed. Five backup safety tests passed. Encrypted database backup/isolated restore scripts and a CI restore rehearsal were added; real restore remains unverified. Discovery found 36 static/six integrated cases across Chromium/Firefox/WebKit. CI YAML parsed locally; remote workflow/container/model execution is not yet verified. Local Gitleaks release download timed out, so its full scan has not run; no scan success is claimed.

See TEST_REPORT.md and DECISIONS.md for current evidence and setup clarifications. The original local-handover record below is historical.

Base: `main` at `da7af55993e2b6dc8d41f46bd621bc10af5b97d5`. Local branch: `codex/backend-integration`. No push, hosted migration, deployment, bucket-policy change or paid inference performed.

## Current continuation evidence — 2026-11-09

The earlier local milestone below is historical. Current source adds controlled-release authorization, atomic inference budgets, administrator operational summaries, explicit per-content-version AI input review, Cairo DST-safe statistics, media-backup tooling, VM packaging, and expanded CI verification. The current public-deployment constraints and account steps are in [HOSTING_SETUP.md](HOSTING_SETUP.md).

- Full local backend: **92 passed, zero failed/skipped**, `evidence/backend-release-controls.trx`. Includes PostgreSQL-backed release restrictions, quota concurrency, reviewed AI privacy/stale-result guards, and statistics date/department cases. All seven migrations applied to isolated local test databases; latest additions also applied to the development database. Hosted schema remains untouched.
- Frontend: **54 passed**, typecheck, lint and build passed. Includes multi-scope receipt permissions and native browser JSON failures. Generated Playwright reports were added to lint exclusions after a local browser attempt exposed accidental scanning of vendor report assets.
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

## Non-AI release and demo setup — 12 November

- Changed the release-policy fallback to accept all syntactically valid email addresses on the exact `o6u.edu.eg` domain in production; explicit `Release:Mode=controlled` remains supported. Verification is still required before sign-in. The Resend test recipient now restricts only the shared `@resend.dev` sender.
- Added admin-only `/api/v1/admin/demo/status` and `/api/v1/admin/demo/prepare`. Preparation is disabled unless `Demo:PreparationEnabled=true`. It creates five distinct fictional accounts, two demo departments, a fictional faculty/building/floor, one maintenance report, two synthetic private found-item photos, and two item-specific 30-day recovery grants. New credentials are returned only on the first successful call; repeat calls do not return them again. AI and general search remain disabled.
- Added integration tests for disabled/unauthorized access, account roles, synthetic records, grant expiry, idempotence, requester visibility and outsider/media denial. They pass against isolated PostgreSQL 16 + pgvector.
- `dotnet build backend/SmartCampus.slnx --configuration Release`: passed, 0 warnings/errors. `dotnet test backend/SmartCampus.slnx --configuration Release`: 178 passed, 0 failed/skipped.
- Frontend: `npm test -- --run` — 65 passed; `npm run typecheck`, `npm run lint` and `npm run build` passed. Playwright Chromium/WebKit: 28 passed. Firefox: 14 could not launch on this Windows host (`spawn UNKNOWN`), before test bodies executed.
- `git diff --check`: passed. No hosted demo data/settings, DNS records, email provider state, push, or deployment were changed here. Full encrypted database backup is still unavailable on the current free hosted database plan; do not enable demo preparation on the hosted site until a complete backup and restore path exists. Real sender/domain authentication and delivery remain unverified.

## Staff maintenance AI review UI — 12 November

- Replaced the obsolete “AI deferred” staff detail panel with the existing server-gated review flow. Staff must explicitly confirm that report text is non-sensitive and approved for external processing; image inclusion requires a separate optional confirmation. The UI presents completed output as an AI suggestion, separate from the staff operational decision, and reports queued/unavailable states without fabricating output.
- Exposed the already-persisted model explanation only with the existing staff-scoped assessment response and updated the API schema additively.
- The API still controls role, report scope, content version, provider budget, approval and image policy. No provider keys, environment variables, search gates, or deployment settings were changed.
- Frontend: `vitest run` — 67 passed; `tsc --noEmit`, targeted ESLint, Vite production build, and `git diff --check` passed. Added `requires explicit text approval before staff can enqueue AI analysis`, `sends an image only after separate opt-in and displays AI suggestions apart from staff decisions`, and `does not offer AI submission when the server blocks external processing`. Backend `dotnet test backend/SmartCampus.slnx --configuration Release` compiled and ran 122 passed, zero failed, 56 skipped; database-dependent tests skipped because no isolated test database is configured. `Reviewed_analysis_stays_private_and_never_sets_staff_decision` now asserts the model explanation is returned to staff with the suggestion.
- Live NVIDIA/OpenRouter provider calls and independent retrieval calibration were not completed: the admin synthetic diagnostics were capped before network calls, and no isolated PostgreSQL test connection is configured locally. Accordingly no live-model success, calibration approval, push, or deployment is claimed; general lost/found search remains closed.
- Follow-up live admin inspection after sign-in verified model routing `auto`, both server keys present, both app budgets configured, external text/image approvals enabled, and maintenance taxonomy incomplete. The dashboard showed application budget use OpenRouter 2/50 and NVIDIA 5/50. The fixed OpenRouter diagnostic returned its once-per-UTC-day cap, and the fixed NVIDIA classification returned its five-per-day admin diagnostic cap before either provider call; neither result proves provider connectivity. Search privacy approval is disabled and calibration is unapproved. No environment values were changed.
- Added broad project-derived maintenance suggestion labels to version-controlled defaults and the example configuration, leaving all host environment values untouched. Existing environment values override those defaults. This resolves the taxonomy-missing readiness condition on the next application deployment; no live deployment or provider result is inferred from the source change.

## Recovery-list query follow-up — 10 November

Replaced per-row found-item/requester lookups in LostFoundService.Claims with one paginated joined read and reused the existing privacy/action projection. Actor authorization, department scope, descending creation order and response fields are preserved; no new data is made available. Other maintenance/found-item list paths still need independent query work. No deployment capacity or latency claim follows from this change.

Regression `Recovery_page_uses_constant_reads_and_preserves_scope_and_collection_privacy` runs against real PostgreSQL and counts actual EF reader commands. The original source failed: one-row page used six reads versus 28 for twelve rows. The joined source passes the constant-read assertion, preserves requester contact redaction until approval, custodian actions, administrator visibility and cross-account/department exclusions. Full backend run: 98 passed, zero failed/skipped (`evidence/backend-recovery-list.trx`). Code/security/test/documentation guard review passed. Initial test-only JSON wrapper equality was corrected to compare GUID values; the original source was then separately proved to fail on read-count growth. No assertions were weakened.

The prior Resend commit 28b8bd1 passed CI 42723719309 (97 backend, 54 frontend, 36 static and nine integrated browser cases, restore/policy/packaging checks); worker run 42723719163 passed. Those runs predate this query follow-up. The current real-email test remains blocked on local API startup/private key entry; no real send or owner verification is claimed.

## Maintenance-list query follow-up — 10 November

MaintenanceService.List now reads the authorized page once and batches related history, media IDs, locations, current assessments and eligible assignees. Detail and list share the same response projection. Staff-related queries are restricted to reports/departments where the current actor has maintenance/admin authority; owning an out-of-scope report never grants staff assessment/decision/assignee fields. Assessments are matched to the loaded report's content version. Mutation locks, version checks, audit and notification writes are unchanged.

`Maintenance_page_batches_reads_and_keeps_assessments_and_assignees_within_staff_scope` uses real PostgreSQL and an EF command interceptor. The original implementation failed: one owner report used eight reader commands versus 52 for twelve reports. The new implementation passes constant-read assertions for both owners and staff, preserves history/location/media mappings, excludes object keys and confidential assessment/history content, restricts assignees and keeps current assessments separate from stale versions. Cross-account/department and mixed staff-owner cases pass. Full backend: 99 passed, zero failed/skipped (`evidence/backend-maintenance-list.trx`). No hosting latency/capacity claim is made.

Code, security, test and documentation review passed. Found-item inventory lists still use per-item detail reads and remain an independent performance follow-up. Real email is still pending the private launcher startup/key setup; no real send, delivery or owner verification is claimed. External deployment and calibration gates remain open.


## 13 November 2026 — OpenRouter-only maintenance text routing and diagnostic

- The MonsterASP private environment now has `AI__Provider=openrouter` (previous value `auto`) and the AppPool was restarted. No API key, budget, privacy switch, search approval, or database value was changed. The routing change means text and staff-approved images use the pinned `nvidia/nemotron-3.5-super-vl:free` model; unused NVIDIA credentials/budgets remain in place but are not selected.
- OpenRouter Chat Completions payloads pin the `:free` model, set `allow_fallbacks=false`, and set `max_price.prompt` and `max_price.completion` to zero. Chat responses must match the requested free ID. Embedding requests also pin `nvidia/nemotron-3.5-embed-8b:free`, disable provider fallbacks and set a zero price cap; the response validator accepts only that variant or its canonical base ID because OpenRouter may normalize the response `model` field. Other model IDs remain rejected. Application per-day/per-minute budgets still apply; provider free-tier quotas and availability are external limits.
- The prior admin OpenRouter diagnostic only exercised classification with image content plus text/image embeddings. The source now validates separate text-only classification, image classification, and both embeddings (four bounded requests) and returns only validated category labels/model IDs. Real records remain outside the diagnostic; existing staff-review and external-processing gates are unchanged.
- Security review: the route remains controller-authorized and explicitly administrator-only, and POSTs are covered by global antiforgery validation. Inputs are fixed in server code; caller fields are ignored. The test path uses a generated synthetic image, validates model output against the allowed taxonomy, returns only category values/model IDs, pins the free model, blocks provider fallbacks and sets max prices to zero. Real maintenance transfer gates and public search calibration remain unchanged.
- Local validation before deployment: isolated PostgreSQL 16 + pgvector suite passed 182/182; frontend passed 67/67, typecheck, lint and build. These results precede the follow-up embedding-response adjustment below.
- The deployed four-request synthetic diagnostic ran after deployment. Text and image classifications passed model/taxonomy validation; the subsequent text embedding was rejected as an invalid embedding, so the image embedding step did not run. This live failure is consistent with OpenRouter normalizing the embedding response model ID, but the response details are intentionally not exposed by the application and the cause is not confirmed. No real report or image was sent.
- Follow-up: accept the canonical embedding model ID only alongside the request's pinned `:free` model, disabled provider fallbacks and zero maximum price; unrelated model IDs still fail closed. Added test coverage verifies the constrained request and rejects unrelated model IDs. Re-ran `dotnet test backend\SmartCampus.slnx` against a fresh disposable PostgreSQL 16 + pgvector database: 182 passed, 0 failed, 0 skipped. Frontend code was not changed in this follow-up.
- Commit `df2503f` passed the GitHub frontend/backend/build/deploy workflow and was deployed to MonsterASP. The live OpenRouter diagnostic then passed all four bounded synthetic calls: text classification and image classification both returned validated `plumbing / medium / medium`; text and image embeddings returned dimension 1024 and revision `nemotron35-8b-openrouter-1024-v1`. This confirms the currently deployed model routing only for these fictional diagnostic inputs. No real report/image was sent; public search privacy approval and calibration remain unchanged, so general search stays closed.

## AI Evaluation page — 13 November 2026

- Added administrator-only `/admin/ai-evaluation` with persisted run snapshots, explicit preflight counts, start/resume/pause, per-case input/reference/model output/state/error/timing, synthetic images, retrieval candidates and Top-1/Top-5 when measured, provisional agreement metrics, charts, separate historical aggregates, CSV, and A4 Browser Print/Save as PDF. Print includes all cases in the selected run and historical records regardless of on-screen filters. Refresh is read-only.
- Suites use fixed fictional inputs: integration 4; text maintenance 12; synthetic maintenance images 4; lost-and-found 20 (6 separate text and image gallery embeddings plus 8 queries). Preflight subtracts only persisted vectors with the current model revision. Requests run sequentially with pinned OpenRouter `:free` models, `max_price=0`, `allow_fallbacks=false`, application daily/minute limits, and no retry. Completed outputs and vectors persist in Postgres. An expired in-flight call becomes unknown/interrupted and is never automatically resent; explicit resume continues pending cases only.
- Added three additive `smart_campus` tables and explicit migration `20261113013915_AiEvaluationResults`; down migration drops only those evaluation tables. Fresh isolated PostgreSQL 16 + pgvector migration succeeded. Existing tables are not recreated. Production backup availability has not yet been confirmed; no backup is claimed.
- Fake-provider tests assert free model IDs, zero price caps, disabled fallbacks, admin access, call counts, persisted output, and no duplicate requests after refresh/resume. They do not establish real model quality or live provider availability.
- Local validation: after rebasing over eight newer commits on the same branch, `dotnet test backend/SmartCampus.slnx --configuration Release` passed 188/188 (49 seconds); frontend ESLint, TypeScript, Vitest passed 67/67 and Vite production build passed. `npm audit --audit-level=moderate` found zero vulnerabilities before the rebase; rerun it on the merged head. `dotnet ef database update` succeeded on fresh isolated PostgreSQL 16 + pgvector. The reviewed additive evaluation tables migration is applied to the hosted database; existing table counts matched the pre-migration baseline. Commit `7344de4` passed CI and MonsterASP deployment. Brave rendering/PDF contents and a live OpenRouter persisted run remain pending. The runtime table-permission follow-up is documented below and still needs CI/deployment before completion.

## AI Evaluation database permission fix — 13 November 2026

- MonsterASP ASP.NET Core stdout identified the page failure: PostgreSQL `42501 permission denied for table AiEvaluationRuns` while expiring interrupted runs. The new tables were owned by `postgres`, but the site's database role `smart_campus_runtime` had no table grants. Existing application tables already grant that role read/write access.
- Added explicit EF migration `20261113023724_AiEvaluationRuntimePermissions`. It grants only `SELECT`, `INSERT`, and `UPDATE` on `AiEvaluationRuns`, `AiEvaluationCases`, and `AiEvaluationVectors` to `smart_campus_runtime` when that role exists. It grants no `DELETE`, no privileges to `anon`/`authenticated`/`PUBLIC`, and its Down path revokes only these privileges.
- Applied the same reviewed grant to production and recorded the EF migration version `10.0.14`. Verified the role has SELECT/INSERT/UPDATE and no DELETE on all three tables; each table remains empty. No existing site rows were modified.
- After the production grant, the live admin evaluation page loaded its stored snapshot. A subsequent inspection showed an existing paused lost-found run with 20 saved cases: 2 provider-unavailable failures and 18 pending cases. This is a persisted partial run, not a successful retrieval evaluation. No cases were resent during this fix.
- Visual follow-up makes charts responsive without a fixed-width SVG, collapses case details by default while keeping input/expected/actual available on expansion, and keeps all case bodies expanded in print. It also improves mobile card spacing and printed chart colors.
- Validation for this follow-up: `npm run typecheck`, `npm run lint`, `npm test` (67/67), and `npm run build` passed. `dotnet test backend/SmartCampus.slnx --configuration Release` passed 188/188 against isolated PostgreSQL 16 + pgvector after adding its `extensions` schema to the test-only search path. Local `npm run test:e2e` could not launch because browser binaries are absent (42 launch failures across Chromium/Firefox/WebKit). GitHub CI then installed the browser engines and passed its regression and integrated browser journeys. Commit `ca84ba95f7c6cd737530413c852fedebd0c7a755` deployed to MonsterASP successfully. Public asset checks confirm the deployed bundle contains the responsive chart markup and collapsed case layout. A post-deploy browser reload expired the site's admin session, so the protected page's updated appearance and PDF preview still need a signed-in Brave inspection.

## 13 November 2026 — Local implementation continuation (not released)

- Removed the AI Evaluation page and navigation/API client references from the operational frontend. Historical evaluation tables, migrations, and stored records were left untouched; the corresponding API is blocked outside Development. Removed synthetic AI diagnostic controls and demo-preparation routes from the operational UI/API registration. Existing demo-account warning text remains for already provisioned accounts.
- Connected the existing protected lost-item search screen to its text/image search API. The screen distinguishes no qualifying matches from calibration, quota, index, privacy, and provider failures; it displays only authorized eligible records and does not invent a match score. Tests verify submitting text plus an image and that calibration failure is not shown as an empty result.
- Pinned maintenance text and approved-image classification to OpenRouter's free `nvidia/nemotron-3.5-super-vl:free` model. Requests specify zero input/output price caps and disable provider fallbacks. Legacy `auto`/NVIDIA selection is rejected; image review still requires existing explicit per-image approval. No live provider request or host environment change was made in this continuation.
- Found-item indexing now checks for persisted vectors at the current model revision before provider calls. Each stored description/image vector is reused on a resumed worker job. A regression was added to `Hosted_description_only_indexing_and_search_work_without_worker_or_photo_disclosure`; this database-backed test was skipped locally because no isolated PostgreSQL test connection was configured.
- Frontend: `npm test -- --run` — 66 passed; `npm run typecheck`, `npm run lint`, and `npm run build` passed. `npm audit --omit=dev --audit-level=moderate` found 0 vulnerabilities. `pnpm audit --prod` was not applicable because the repository has no `pnpm-lock.yaml`.
- Backend: `dotnet test backend/SmartCampus.slnx --configuration Release` — 127 passed, 0 failed, 50 skipped. Skipped cases require `SMART_CAMPUS_TEST_DB`; port 5433 was unreachable and the variable was absent. No integration database was created, and no remote/hosted database was used. The initial run exposed a missing free-model ID in a synthetic-response fixture; it was corrected and the local command then passed.
- Interactive visual acceptance did not complete: the user stopped Brave automation with Escape. No Brave screenshots for this continuation, no complete route review at 390/768/1440, no browser E2E run, no live OpenRouter evaluation, and no production verification are claimed. Full 55-requirement acceptance remains open. No commit, push, merge, or deploy was performed.

## Local acceptance continuation — 13 November 2026 (unreleased)

- Backend: the complete suite passed against a newly created disposable PostgreSQL 16 + pgvector cluster isolated from hosted databases: 177 passed, 0 failed, 0 skipped. The temporary database cluster and loopback forwarder were stopped and removed after the run. This supersedes the earlier 127 passed / 50 skipped result above.
- Frontend: `npm test -- --run` passed 67/67 after adding a regression for failed session restoration; `npm run typecheck`, `npm run lint`, and `npm run build` passed. `npm audit --omit=dev --audit-level=moderate` found zero production dependency vulnerabilities. No dependency changes were made.
- Brave manual inspection used the local Vite site only. Home, sign-in/combined registration, standalone registration, recovery and verification were visually inspected. Responsive home layouts were inspected at 390, 768 and 1440 CSS pixels; English and Arabic were checked. Empty registration submission produced the browser's required-field validation and sent no form data. No visual defect requiring before/after screenshots was found in the inspected public/auth views.
- The local Vite proxy returned HTTP 500 for `GET /api/v1/auth/me` because the API was not running on the configured local port 5080. The frontend now shows an explicit bilingual service/session-status notice instead of presenting the failed session check as proof of a guest session. A regression test covers this state.
- Brave-authenticated workflows, staff/admin views, protected route visual coverage, all page/interaction combinations, live OpenRouter quality or retrieval calibration, email receipt/authentication headers, and production behavior were not tested. No real provider requests, real student data, hosted DB/API changes, commit, push, merge, or deploy occurred. Full 55-requirement acceptance is not established.

## Automated/source review continuation — 13 November 2026 (unreleased)

- Reviewed the approved 55-row traceability matrix and the current React routes, role guards, API controller surface, retrieval gates, and maintenance AI consent gates. The supported route tree contains operational auth, maintenance, found-item, custody/recovery, admin, notification, and statistics pages; there is no AI Evaluation route. Search remains intentionally unavailable unless hosted provider, privacy approvals, vector revision, and calibration approval are all present. No production config was changed to bypass those controls.
- Improved keyboard operation of the admin building-position editor and added a component regression for keyboard placement and versioned saving. The test initially used a Vitest matcher that is not installed; it was corrected to assert the native `disabled` property, then passed.
- Removed orphaned AI Evaluation screen, chart, case, responsive, and print CSS after confirming no operational React component uses those selectors; retained the general admin print rules. Frontend suite rerun after removal passed 68/68, and typecheck, lint and production build passed.
- Repeated matching refreshes an existing per-user result grant in place, including after it expires, instead of inserting another temporary authorization row. The hosted retrieval integration regression repeats the retrieval-service flow before and after expiry and verifies one refreshed grant remains.
- Hosted vector resumption now trusts a saved embedding only when the stored dimension matches the pinned model's 1024 dimensions. A regression corrupts both vector and metadata consistently, requeues indexing, and verifies a provider result repairs it; the pgvector dimension constraint remains satisfied throughout.
- Corrected the local test DB connection to the dedicated loopback `_test` database and role after an initial attempt with the wrong credentials failed before the fixture could execute its schema reset. Following the retrieval-grant and vector-dimension fixes, the complete Release backend suite passed: 177 passed, 0 failed, 0 skipped; the focused retrieval test also passed independently. Frontend suite: 68 passed; typecheck, lint and production build passed. Release backend build: 0 warnings, 0 errors. `npm audit --omit=dev --audit-level=moderate`: 0 vulnerabilities. `git diff --check`: passed (Git reported existing LF-to-CRLF normalization notices only).
- No Brave/manual browser inspection, local website launch, real OpenRouter request, live-site test, commit, push, merge, or deploy was performed in this continuation. Visual layout and authenticated end-to-end acceptance are deferred; full 55-requirement completion is not established.

## Found-item list continuation — 13 November 2026 (unreleased)

- Source review found that `LostFoundService.List` called `Detail` for every row (up to 100 rows), issuing several additional database reads per item. It also omitted items readable by a requester through an existing recovery claim, despite `Access.ItemReadable` permitting that access.
- Batched found-item list materialization and related handover, media, department, temporary-grant, and active-claim reads. The list filter now includes claimed items. Server-side authorization conditions are preserved, and response fields still exclude storage object keys and custodian contact details.
- Added `FoundListTests`: for a 1-item and a 100-item page, reads are equal and capped at ten; tests verify uploader, current custodian, latest pending handover, receiving department, existing claimant, unrelated outsider, and admin visibility. Complete Release backend suite passed 178/178, and the focused test passed again after tightening its read ceiling and handover-history fixture.
- Batched the admin account page's role, scope, and synthetic-account marker reads instead of issuing those queries per account. `AdministrationAccountsListTests` verifies a one-row and full page use the same capped query count and retain employee roles/scopes; the complete Release backend suite now passes 179/179.
- No browser, live provider call, hosted environment, or production database was used. UI visual and authenticated end-to-end acceptance remain outstanding.

## Follow-up directory and automated checks — 13 November 2026 (unreleased)

- `DirectoryController.Locations` previously rescanned the full locations array for each faculty and each building. It now groups rows by parent once and builds the same faculty → building → floor response from those groups. This removes the repeated full-array scans without changing persisted data or the API's hierarchy.
- Added `DirectoryTests.Location_tree_groups_children_by_parent_and_does_not_cross_faculty_boundaries` and `DirectoryTests.Location_tree_returns_empty_collection_without_locations`; focused run passed 2/2.
- Current automated runs: `dotnet test backend/SmartCampus.slnx --configuration Release` — 129 passed, 0 failed, 52 skipped; skipped tests require `SMART_CAMPUS_TEST_DB`, and no local PostgreSQL server was available on the expected loopback port. `npm test -- --run` — 68 passed; `npm run typecheck`, `npm run lint`, and `npm run build` passed.
- Visual/Brave checks and browser E2E were not run, per the user's instruction. No local website was started. No hosted database, provider, live site, commit, push, merge, or deployment was used.

## Found-item image-list loading — 13 November 2026 (unreleased)

- Found-item lists previously fetched every authorized item's image into a `File` and base64 preview before rendering. Lists now keep the server-provided protected media URL; cards load images lazily with async decoding. Detail/mutation forms that need an editable file continue using the existing authenticated blob-to-File path.
- Added `lost-and-found frontend boundary loads the authorized found-item page without prefetching every private image`, which verifies one list request and no per-image fetch. Focused lost tests passed 3/3. Full frontend tests passed 69/69; `npm run typecheck`, `npm run lint`, and `npm run build` passed.
- No browser or visual check was performed. This optimization does not change API visibility or authorization; the image endpoint continues enforcing access on each browser request.

## Notification read-state interaction — 13 November 2026 (unreleased)

- The notification page now increments its refresh revision functionally after marking a notice read, so overlapping updates cannot reuse a stale captured revision.
- Added `marks an owned notification read and refreshes its state` in `tests/protected-forms.test.tsx`; it verifies the account-scoped action and refreshed read status.
- Final frontend run for this change set: 70/70 tests, typecheck, ESLint and Vite production build all passed. No browser inspection was performed.

## Found-item custody privacy — 13 November 2026 (unreleased)

- Search-grant holders and recovery requesters could previously receive the item's internal storage location and pending handover office in the found-item projection. The API now reveals storage only to the current custodian or an administrator, and handover details only to the current custodian, an administrator, or the authorized receiving department. The UI omits the custody-location section for potential claimants.
- `FoundItem.storage` now models the API's nullable/redacted value. Added `does not expose a found item's custody location to a potential claimant`; focused UI tests pass 17/17. `FoundListTests.Found_item_page_batches_reads_and_includes_claimant_and_receiving_department_access` now also asserts claimant redaction while the holder, receiver and admin retain their appropriate fields; this database-backed test still requires an isolated PostgreSQL instance.
- Verification: full frontend tests 71/71, typecheck, ESLint and production build passed. `dotnet test backend/SmartCampus.slnx --configuration Release` compiled the changed API/tests and passed 129 tests, with 52 database-backed tests skipped due to unavailable PostgreSQL. No production database or user data was touched.

## API input bounds — 13 November 2026 (unreleased)

- `MaintenanceService.Update` now normalizes nullable notes and rejects content over the database's 4,000-character string limit before writing history. Rejection reasons remain required; ordinary update notes may stay blank. The textarea has the matching `maxLength=4000` bound.
- Added `RuleTests.Optional_text_accepts_empty_and_boundary_values_but_rejects_oversized_content`; focused backend run passed 15/15. Frontend regression checks the textarea bound; focused protected-form run passed 17/17. Full UI checks remain 71/71 with typecheck, lint and build passing.
- No schema or hosted data change was required. PostgreSQL API integration coverage remains deferred until an isolated test database is available.
- Matched client-side maximum lengths to backend rules for maintenance description/location and found-item description/place/storage, handover office, recovery message/reason/instructions, collection location and contact phone. UI regressions check maintenance description/location and found-item description/place/storage/office limits. Full frontend suite passes 71/71; typecheck, lint and production build pass.

## Maintenance assignee clearing — 13 November 2026 (unreleased)

- The staff assignment dropdown offered “Unassigned”, but an explicit empty assignee was ignored by the service. The update now clears the assignment for an explicit empty value; an omitted/null field preserves the current assignment. The existing “repair requires an assignee” guard remains after this change.
- Added `submits an explicit empty assignee when staff selects unassigned` (frontend) and `ApiTests.Staff_can_unassign_a_report_but_cannot_start_repair_without_an_assignee` (API integration). Frontend test passes. The API regression compiles but was skipped because it requires an isolated PostgreSQL database unavailable in this environment.

## Continued source review — 13 November 2026 (unreleased)

- Maintenance assignment regression is now covered end-to-end at the form boundary: choosing “Unassigned” submits an explicit empty assignee; the backend clears assignment only when the field is explicitly supplied, preserves it when omitted, and continues to reject transition into repair without an assignee.
- Latest frontend suite: `npm test -- --run` — 72 passed across 12 files. `npm run typecheck`, `npm run lint`, and `npm run build` passed. Latest backend suite: `dotnet test backend/SmartCampus.slnx --configuration Release` — 130 passed, 0 failed, 53 skipped. The skipped cases require an isolated PostgreSQL test database (`SMART_CAMPUS_TEST_DB`); none were run against production or a hosted database.
- Source scans found no `href="#"`, no empty `onClick={() => {}}` handlers, no TODO markers, and no `NotImplementedException` in `src` or `backend`.
- `git diff --check` passed. Work remains on `codex/release-readiness-20261111`; no commit, push, merge, deploy, local server, live provider call, or browser session was started.
- Required Brave interactive visual acceptance remains deferred by user instruction. Requirement-by-requirement acceptance of all 55 project requirements is not established by these checks; PostgreSQL integration workflows and production provider/email delivery also remain unverified in this continuation.

## Notification recipient scope follow-up — 13 November 2026 (unreleased)

- Department custody notifications previously included any scoped account with the department role, even if the account was deactivated or its email was unconfirmed. Both initial handover notifications and notifications to current departmental custodians now require an active, email-confirmed account in addition to the existing department role and scope.
- Added `ApiTests.Custody_notifications_exclude_inactive_or_unverified_department_accounts`, covering both handover and recovery-request notifications. Its first run without a test database was skipped; it later passed against isolated PostgreSQL as part of the full 185-test run below.
- Security review of the changed notification path checked recipient authorization/scope, parameterized EF queries, safe response content and absence of secret/log changes. No schema, production data, or permission grants changed.
- Latest automated results after this change: backend 130 passed, 0 failed, 54 skipped; frontend 72 passed; typecheck, lint and production build passed. The extra skipped test is database-dependent. `git diff --check` is pending final status review.

## Isolated PostgreSQL verification — 13 November 2026

- Re-ran `dotnet test backend/SmartCampus.slnx --configuration Release` with a fresh temporary PostgreSQL 16 + pgvector database on local WSL, owned by a unique temporary test role. The full suite passed: 184 passed, 0 failed, 0 skipped. This includes the new notification recipient integration test and the previously affected assignment/concurrent-approval tests.
- The initial full run exposed test-fixture state leakage: the notification test deactivated a shared fixture user without restoring it, causing two later tests to fail. The test now restores the account in `finally`; the new test passed 1/1, both affected tests passed 2/2, and the full suite passed after the fix.
- The temporary database and role were dropped after the successful run. No hosted database, local application server, Brave, real user data, or external AI provider was used.

## DSS date-boundary verification — 13 November 2026

- Added `StatisticsTests.Date_filter_includes_cairo_day_boundaries_and_excludes_the_following_midnight`. It verifies date filtering across the Cairo DST transition includes both instants within the selected local day, excludes the prior/following boundary records, computes recurrence from only the filtered records, and rejects an inverted date range.
- Focused integration test passed 1/1. The final full PostgreSQL 16 + pgvector backend run passed 185/185, 0 failed, 0 skipped. The temporary test database and role were removed after the run.

## Latest verification and bounded OpenRouter probe — 13 November 2026

- Full Release backend suite: **187 passed, 0 failed, 0 skipped**, using a temporary isolated PostgreSQL 16 + pgvector database. The database and role were removed afterward; no hosted database was used.
- Dedicated DPAPI-protected OpenRouter test key only; production configuration/key was not read or changed. The key endpoint reported a free-tier key. Every inference payload pinned the named `:free` model, set `max_price` to zero, disabled provider fallbacks, and used no request retry. Inputs were fictional text and generated illustrations only.
- Maintenance text: after two bounded malformed/truncated probe responses (HTTP 200, one with `finish_reason=length` and no JSON content), a probe matching the app's `reasoning.enabled=false` setting returned valid allowed JSON in **698 ms**: `plumbing`, `high`, `high`. This establishes that this exact text request can produce a parseable classification; it is not an accuracy evaluation.
- Maintenance image: a generated pipe-leak illustration returned valid JSON in **1,111 ms**: `plumbing`, `high`, `high`. A separate irrelevant bottle illustration was classified as `safety/high/high`; it is excluded from evaluation because it was not a maintenance-fault fixture and illustrates the need for curated image acceptance cases.
- Lost & Found embeddings: four one-shot synthetic requests with `data_collection=deny` returned HTTP **404**. With `allow` for synthetic-only data, a text-passage request returned HTTP **200**, normalized **1024 dimensions**, **1,082 ms**; a separate image-passage request returned HTTP **200**, normalized **1024 dimensions**, **1,075 ms**. The A/B confirms that the routing privacy constraint caused the observed deny failures and confirms the configured dimension. An additional retrieval-pair harness's first request returned HTTP 400 and stopped without retry; its response body was not retained. Candidate ranking, mixed-language retrieval quality, no-match false positives and calibration remain unverified; search remains closed. OpenRouter documents `deny` as excluding providers that collect data, and this free model's sole provider publishes a logging/data-improvement notice. See [provider routing policy](https://openrouter.ai/blog/insights/model-routing/), [model details and pricing](https://openrouter.ai/nvidia/nemotron-3.5-embed-8b:free), and [embedding API reference](https://openrouter.ai/docs/api/api-reference/embeddings/submit-an-embedding-request).
- Added an explicit `Search:ProviderDataCollection=allow` gate to the hosted embeddings configured check. Missing/deny policy now fails before API quota reservation or external transmission. The default remains deny. To use this specific free provider, an authorized operator must explicitly approve the provider's collection policy as well as external processing; this test approval covered only fictional data, and no production setting was changed.
- The encrypted test-key file remains outside the repository at `%LOCALAPPDATA%\SmartCampusTests\openrouter.key.dpapi`; the automatic review rejected the attempted deletion. The key was only decrypted into process memory for the calls and was never printed or added to the repo. Generated synthetic fixtures remain in the OS temp folder for the same reason. No browser, app server, real user data, email, production setting, commit, push, merge, or deployment was used.

## Provider privacy-route fix and re-verification — 13 November 2026

- A controlled one-request synthetic text-embedding probe with `data_collection=allow` returned HTTP 200, model `nvidia/nemotron-3.5-embed-8b`, a normalized 1024-dimensional vector, and 1,082 ms latency. Compared with the four 404 responses under `deny`, this confirms the route policy was the cause for those requests. OpenRouter says `deny` excludes data-collecting providers; the free model's NVIDIA endpoint publishes data collection/logging. No student data was sent.
- `RemoteEmbeddings.Configured` now requires a separate explicit `Search:ProviderDataCollection=allow` setting, in addition to existing external processing consent. Missing/deny fails before quota reservation or outbound HTTP. Example/default configurations explicitly remain `deny`; test fixtures opt into `allow` only for synthetic fixtures. No production setting was changed.
- Added regression `RemoteEmbeddingsTests.Unapproved_provider_data_collection_never_spends_budget_or_sends_a_request` (deny and missing policy cases). Focused `RemoteEmbeddingsTests` passed **15/15**; `ProviderTests` passed **43/43**. The complete PostgreSQL 16 + pgvector Release backend suite then passed **189/189**, 0 failed, 0 skipped. Its first setup attempt failed because the new isolated `_test` database lacked the installed vector extension; after installing pgvector inside that test database only, the full suite passed.
- The test database `smart_campus_codex_20261113_followup_test` and role `codex_sc_followup_20261113` were created for the run. The automatic review rejected the requested database/role removal. The role has since been changed to `NOLOGIN NOSUPERUSER`; the test database and temporary connection-string file remain local. They contain test data only and are not connected to the hosted project.
- Retrieval text/image embedding requests now each have one successful synthetic live example. Candidate ranking, mixed-language accuracy, no-match calibration, UI/live-browser workflows and production data governance remain unverified. Search remains closed; the explicit `allow` gate is not enabled in production.
- Security package checks: `dotnet list backend/SmartCampus.slnx package --vulnerable --include-transitive` reported no vulnerable packages in the four solution projects; `npm audit --omit=dev` reported zero vulnerabilities.
- Frontend after the backend policy gate: Vitest **72/72**; `npm run typecheck`, `npm run lint`, and `npm run build` all passed. This was code/build verification only; no browser session was started.

## Separate test-key follow-up — 13 November 2026

- Decrypted the DPAPI-protected test key only in process memory and sent one fictional embedding request, pinned to `nvidia/nemotron-3.5-embed-8b:free` with `max_price=0` and `allow_fallbacks=false`. OpenRouter returned HTTP **401**. The request was stopped without retry; no embedding or retrieval score was produced. The saved test key is not currently accepted. The production key was not accessed or changed.
- OpenRouter documents a free-model cap of **50 requests/day and 20/minute**, with failed attempts counting against the cap. The exact remaining daily allowance was not exposed, so no further provider requests were sent after this 401. See [OpenRouter free model limits](https://openrouter.ai/collections/free-models/).
- The HTTP 401 attempt had zero model price configured and did not execute inference. This does not establish successful AI functionality, ranking quality, or accuracy. Search remains gated closed. No data other than fictional test input was sent, and no browser, production setting, commit, push, merge, or deployment was used.

## Multi-role navigation regression — 13 November 2026

- The work-queue and admin links in the primary navigation previously checked only the account's primary role, while route authorization already accepted additional granted roles. Navigation now uses the same primary-or-granted-role rule; work-queue role selection also recognizes secretariat custody scope. This changes discoverability only; server authorization remains authoritative.
- Added `shows work queue and admin navigation for additional authorized roles`, `does not show staff or admin navigation for an ordinary user`, and `shows custody queue switching to a secretariat user with another staff role` in `tests/navigation-roles.test.tsx`.
- Verification: focused navigation tests passed **3/3**; full `npm test -- --run` passed **75/75** across 13 files; typecheck, lint and production build passed. No browser was used. No backend/API/data or production configuration changed.

## Maintenance provider data-collection gate — 13 November 2026

- The free Super VL endpoint's published terms disclose logging for security/product improvement and warn callers not to submit confidential or personal information. Maintenance routing now requires explicit `AI:ProviderDataCollection=allow` in addition to `AI:AllowNonSensitiveInference`, report-level user confirmation, staff review, and the independent image gate. The request passes this policy explicitly; omitted/deny fails before the budget reservation. Defaults and example configuration remain deny. No production settings changed.
- Admin operations reports the policy as a non-secret readiness field; the Arabic/English operations view displays both denied-by-default and explicitly approved states. The API addition is backward-compatible for clients because it only adds a response property; the TypeScript field is optional for older deployments.
- `ProviderTests` passed **26/26**, including denial for missing/deny policy before budget/HTTP, and the explicitly allowed request carries `data_collection=allow` with `max_price=0` and `allow_fallbacks=false`. Full backend suite without `SMART_CAMPUS_TEST_DB`: **136 passed, 55 skipped**; all projects compiled. Frontend tests passed **77/77**, typecheck, ESLint, and production build passed. `npm audit --omit=dev` found 0 vulnerabilities; NuGet audit found no vulnerable packages. The operations UI also distinguishes denied, approved, and unreported policy state for older API responses.
- OWASP source review: no change to identity, roles, or object access; fixed provider URL and fixed free model remain server-owned; JSON serialization and response allowlists remain; no provider payload/key logging was added; no package or schema change. Live inference and production processing remain blocked until an authorized operator approves the provider's collection terms; the saved separate test key returned HTTP 401, and no further request was sent.

## Full isolated backend re-run — 13 November 2026 (unreleased)

- Created a new PostgreSQL 16 + pgvector cluster and test database under a task-specific `/tmp` path; it listened only on loopback and was stopped after the run. The test fixture enforces both loopback host and `_test` database suffix. No existing or hosted database was used.
- Initial suite result was 186 passed and 5 failed: the maintenance integration fixture did not opt into the new provider data-collection gate, so five tests returned `ai_not_configured` before exercising their assertions.
- Updated only `AssessmentPrivacyTests.Configured()` to set `AI:ProviderDataCollection=allow` for fictional reports, synthetic test key, and a fake HTTP provider boundary. Production defaults remain deny. The full Release suite then passed **191/191** with no skips.
- The current saved test-key file could not be decrypted by the additional verification shell in this turn; no authenticated OpenRouter call was made in this rerun. The previous properly decrypted one-shot request returned 401, as recorded in the test-key follow-up. Real-provider AI and retrieval acceptance remain unverified.
## Custody role compatibility — 13 November 2026 (unreleased)

- `currentCustodian` and `receiving` now use the common `hasRole` helper instead of falling back to primary-role checks only when the roles array is absent. This keeps legacy/additional secretariat scopes visible to their authorized users while preserving department-scope matching; server-side authorization is unchanged and authoritative.
- Added a regression for an account with primary `user` role, an additional secretariat grant and a matching scope. `npm test -- --run`: **80 passed**; `npm run typecheck`, `npm run lint`, and `npm run build` passed. No Brave interaction was performed.
## Current OpenRouter synthetic smoke — 13 November 2026

- Re-read the saved test key in memory using the matching PowerShell DPAPI SecureString format. OpenRouter `/api/v1/key` accepted it and reported a free-tier key; output omitted key material and remaining free request quota was not disclosed.
- Sent six sequential model requests without retry. All used only `nvidia/nemotron-3.5-super-vl:free` or `nvidia/nemotron-3.5-embed-8b:free`, `max_price=0`, `allow_fallbacks=false`, and synthetic fictional input. `data_collection=allow` applied only to these fictional test payloads; no production secret/configuration was accessed.
- Real maintenance text call: HTTP 200, 940 ms, valid allowlisted JSON `plumbing/high/high` for a synthetic Egyptian-Arabic leak near an outlet. Real maintenance image call: HTTP 200, 2,018 ms, valid allowlisted JSON `plumbing/high/medium` for a generated pipe-leak illustration.
- Real lost/found embeddings: text passage 629 ms and query 2,576 ms; image passage 1,430 ms and query 2,512 ms. All four responses returned 1,024 finite normalized values; the provider canonicalized the returned model name by removing `:free`, which the application explicitly allows while validating the pinned free request. Similarities for one synthetic related-text pair and one same-object drawing pair were 0.64071 and 0.98444 respectively.
- No accuracy, Top-1/Top-5, no-match false-positive or threshold claim follows from one candidate per pair. The search gate stays closed pending independent reviewed calibration. These were direct provider calls and did not exercise the app's durable queue, staff consent workflow, or database persistence. No browser, application runtime, real user data, email, commit, push, merge, or deployment was used.
## Application-code OpenRouter live test — 13 November 2026 (unreleased)

- Added a repeatable opt-in test that invokes the actual `AiProviders` and `RemoteEmbeddings` implementations with a separate test key. It requires both `SMART_CAMPUS_RUN_LIVE_AI_TESTS=true` and `SMART_CAMPUS_OPENROUTER_TEST_KEY`; without them it skips without a network request. It uses an in-memory request budget capped at six calls and has no retry behavior. The six calls use synthetic text and generated images only.
- Focused invocation: `dotnet test backend/SmartCampus.slnx --configuration Release --filter 'FullyQualifiedName~OpenRouterLiveTests' --logger 'console;verbosity=detailed'` — **1/1 passed**. Actual application response: text `plumbing/high/high` (1,703 ms), image `plumbing/medium/medium` (8,369 ms), and four valid 1,024-dimensional text/image passage/query vectors. Only allowed response shape and taxonomy are checked; no classification accuracy is claimed.
- The complete isolated PostgreSQL suite then passed **191 passed, 1 skipped, 0 failed**. The single skipped case is the live provider test because its explicit opt-in variables were removed. PostgreSQL test cluster stopped; no process environment variable for the test key remains.
- All six live-test calls pinned `:free` models, `max_price=0`, and disabled provider fallbacks; no retry or paid endpoint was used. Provider returns canonical embedding model IDs without the `:free` suffix, which the application explicitly accepts only for that pinned request. Current retrieval calibration/Top-1/Top-5/no-match accuracy, real authenticated queue persistence, browser acceptance, hosted workflows, and private-data governance remain open.

## Modality-specific hosted retrieval calibration — 13 November 2026 (unreleased)

- Exploratory synthetic retrieval used 14 successful text-embedding requests and 14 successful image-embedding requests. Text results: positive Top-1 and Top-5 4/4; two no-match queries had max scores 0.32874 and 0.29405; lowest correct positive score was 0.43035. Image results: positive Top-1 and Top-5 4/4; two no-match queries had max scores 0.72054 and 0.71099; lowest correct positive score was 0.97032. The set is small, synthetic, not independent, and unsuitable for production calibration or accuracy claims. It only showed the distributions differ enough that one shared threshold is unsafe.
- `RetrievalService` now selects `Search:TextMinimumScore` for text-only searches and `Search:MultimodalMinimumScore` when a query photo is provided. Hosted search requires both separate values, a non-empty approval reference, and the current model revision before the admin readiness flag indicates calibration is approved. Legacy loopback worker search retains `Search:MinimumScore`. All shipped defaults keep hosted thresholds unset; no production environment variables were changed.
- Added `RuleTests.Hosted_search_requires_independently_approved_thresholds_for_both_modalities` and `RuleTests.Hosted_search_rejects_non_finite_or_out_of_range_modality_thresholds`. Focused `RuleTests` passed 24/24; the full isolated PostgreSQL 16 + pgvector Release suite passed 200, skipped 1 (the explicit opt-in live API test), failed 0. No OpenRouter calls were made for this implementation check; the test database cluster was stopped afterward.
- Browser/visual inspection, current hosted operation, production privacy approval, independent calibration, and full end-to-end retrieval remain unverified. No commit, push, merge or deploy occurred.

## DSS workload metric regression — 13 November 2026 (unreleased)

- Added `StatisticsMetricRegressionTests.Workload_metrics_count_only_non_cancelled_reports_and_average_valid_resolution_times`. Synthetic PostgreSQL records verify totals and status breakdowns including cancellation, cancellation-excluded resolution rate, average of two valid resolution durations, open report count, oldest-open age, staff-confirmed recurrence, and the eight-week trend.
- Focused test passed 1/1. Full `dotnet test backend/SmartCampus.slnx --configuration Release` against isolated loopback PostgreSQL 16 + pgvector passed **201, skipped 1, failed 0**; the only skip is the explicit live-provider test. The temporary cluster is stopped. No live AI request, browser session, production data, commit, push, or deployment was used for this check.

## Current continuation verification — 13 November 2026 (unreleased)

- `dotnet test backend/SmartCampus.slnx --configuration Release --filter 'FullyQualifiedName~OpenRouterLiveTests' --logger 'console;verbosity=detailed'`: **1/1 passed**, with six actual sequential fictional model requests, no retries, all requests using pinned `:free` IDs, `max_price=0`, and `allow_fallbacks=false`. Latencies: maintenance text 8,367 ms; maintenance image 11,039 ms; text passage/query embeddings 11,115/17,005 ms; generated image passage/query embeddings 24,932/24,706 ms. Four vectors had 1,024 dimensions. These calls do not measure accuracy or retrieval ranking.
- Frontend: after localizing known maintenance categories/severity values in AI suggestions and DSS fault summaries, and removing unused synthetic AI smoke calls from the frontend service, **82/82** Vitest tests passed; typecheck, ESLint and production build passed. The existing UI test asserts the readable English label instead of the raw model token. The 42-case Playwright static suite could not launch because Chromium, Firefox and WebKit executables are absent; zero cases reached application assertions. No Brave session was used.
- The key metadata endpoint recognized the locally protected test credential as free tier but did not expose its remaining daily request count. The published platform cap is 50 free-model requests/day and 20/minute; only the six-call bounded application test was run in this continuation. No production settings, data, email, commit, push, merge or deployment were touched.

## DSS priority and risk distributions — 13 November 2026 (unreleased)

- `/api/v1/statistics` now returns staff-entered priority/risk counts under the existing date and department authorization filters. Cancelled reports are excluded; the separate AI assessment entity is deliberately not queried.
- Admin overview renders both distributions with English/Arabic labels and explicitly describes them as staff-confirmed. Additive response fields preserve existing clients.
- `StatisticsTests.Statistics_expose_only_staff_confirmed_priority_and_risk_decisions` passed **1/1**, proving AI-only suggestions are excluded. Full isolated PostgreSQL 16 + pgvector Release suite passed **202, 1 intentional live-provider skip, 0 failed**. Frontend suite passed **82/82**; typecheck, ESLint, and build passed. Test PostgreSQL cluster was stopped after execution.

## Gemini Embedding 2 provider migration — local/unreleased — 13 November 2026

- Replaced only lost/found embeddings with direct Google Gemini API calls to the pinned `gemini-embedding-2` model at 768 dimensions. Maintenance text and image inference remain on the existing OpenRouter Nemotron Super VL path. The Gemini API key is read only from server configuration and sent in `x-goog-api-key`; requests, response bodies, and keys are not logged. No retry or provider fallback is implemented.
- Added bounded provider-specific daily/minute budgets, 15-minute SHA-256-keyed embedding cache, vector normalization/dimension checks, a separate vector revision, and fail-closed calibration. Default production transfer, image transfer, indexing, and calibration approvals remain off.
- Added backend image validation/normalization for JPG, PNG, and WebP: 5 MiB upload limit, 16 MP cap, 2048 px maximum edge, JPEG re-encoding to remove metadata, and a 1.9 MB Gemini payload ceiling.
- Reindex scheduling now catches both missing current-revision descriptions and missing image vectors when both generic and Gemini image-transfer approvals are enabled. It is opt-in, bounded, and restart-safe; completed vectors are reused by the indexing workflow.
- `GeminiReindexerTests.Reindex_queue_is_opt_in_bounded_and_idempotent` and `GeminiReindexerTests.Reindex_queue_includes_items_with_current_text_but_missing_approved_image_vectors` passed **2/2** against isolated local PostgreSQL + pgvector, including the no-image-consent case. Full backend Release suite passed **211 passed, 3 intentional live-test skips, 0 failed**. `RemoteEmbeddingsTests` exercise request shape, key header, cache, privacy gates, errors/no retry, vector validation, and synthetic image conversion.
- Frontend suite passed **82/82**; typecheck, ESLint, and production build passed. The admin map selector now deduplicates buildings shared by its placed and unplaced lists.
- Real Gemini calls remain unverified and were deferred at the user's request. No Gemini request was sent; no test key was stored in the repository or test environment. A synthetic DPAPI round-trip had passed separately.
- Search remains closed pending policy approval, an actual valid replacement test key, successful real synthetic text/image calls, and independent calibration. These results do not measure retrieval accuracy or authorize sending production content to Google.

## Gemini live integration and image upload verification — 13 November 2026 (unreleased)

- The owner ran the local session-only PowerShell runner with a separate test key and supplied its console output: `GeminiLiveTests.Gemini_embedding_2_returns_768_dimensional_text_and_image_vectors_for_synthetic_inputs` passed, total 1, failed 0, skipped 0, process exit code 0, test duration 2.6 seconds. The test calls the application `RemoteEmbeddings` implementation for one fictional Arabic text and one generated image, asserts 768 dimensions and exactly two budget reservations. This supersedes the preceding deferred live-integration status; it does not prove retrieval quality, independent calibration, production configuration, or hosted operation. Per-modality latency and raw vectors were not captured in the supplied output. No test credential was written to the repository or persisted by the session-only runner.
- Added shared frontend image validation for JPEG/PNG/WebP signatures, MIME consistency, nonempty files and the existing 5 MiB upload limit. Image fields block form submission while validating; invalid/unreadable files produce localized feedback. The server remains responsible for full image decoding, pixel limits and normalization. Existing form tests now use actual allowed signatures and await the attachment validation.
- Focused `npm test -- tests/image-upload.test.ts tests/protected-forms.test.tsx` passed 30/30; subsequent full `npm test` passed 90/90 in 15 files. Typecheck, ESLint and production build passed. No interactive visual/browser check was performed. No commit, push, merge, migration, production configuration change or deployment occurred.

## Image-only lost-item search — 14 November 2026 (unreleased)

- Fixed an operational gap: `/found/search` and its form now accept a photo without inventing or requiring a description. Neither input is rejected; text-only behavior remains supported. Image-only cache misses need one image request, combined cache misses need up to two. Invalid query images are rejected before spending a text request.
- Added a separate default-unset `Search:ImageMinimumScore`. Hosted readiness requires independently reviewed text-only, image-only and combined thresholds for the current model revision. Photo queries fail closed while an eligible item's photo vectors are missing. SQL ranking now filters the model as well as revision/dimension; image-only scoring uses image vectors only. Indexing also requires the separate Gemini image approval even when generic image approval exists.
- `HostedImageQueryTests.Image_only_and_combined_queries_rank_current_vectors_without_empty_text_requests` verifies real PostgreSQL/pgvector ranking, per-modality request counts, no-match behavior, result grants, invalid-image rejection without provider spend, and index-pending failure using a clearly synthetic encoder. It is not model-accuracy evidence. `ApiTests.Private_inventory_media_and_uncalibrated_search_do_not_leak` now also covers image-only multipart binding and blank-input rejection. `RuleTests.Hosted_search_requires_independently_approved_thresholds_for_all_three_query_modes` and `Hosted_image_only_search_rejects_invalid_calibration_thresholds` cover calibration gates.
- Full Release backend suite on isolated loopback PostgreSQL 16 + pgvector: **216 passed, 3 skipped, 0 failed**, `backend/SmartCampus.Tests/TestResults/image-query-20261114.trx`. Skips are the opt-in OpenRouter live, Gemini live, and saved-gallery evaluation tests. No provider request was made in this run. The first full attempt found a blank-multipart fixture mismatch; the final run above passed after testing the actual whitespace-description form case.
- Frontend: **92/92 passed**, including `submits an image-only search without requiring or inventing a description` and `rejects a blank search before sending an API request`; typecheck, ESLint and production build passed. Interactive browser/visual validation remains deferred by user direction. No commit, push, deploy, production data/configuration change or migration occurred. Real Gemini retrieval quality and independent calibration remain unverified; no production search activation is justified by these synthetic-vector tests.

## Upload races and bounded worker recovery — 14 November 2026 (unreleased)

- Fixed asynchronous attachment selection: removal/cancellation invalidates pending validation, stale validations cannot overwrite the latest choice, and the UI shows a checking state while submission is temporarily blocked. `does not reattach a photo when removal cancels its pending validation` and `keeps the latest selected photo when earlier validation completes out of order` passed; the focused image-field/form run passed 26/26. Full frontend suite then passed **94/94** in 16 files. Typecheck, ESLint and production build passed.
- Fixed the durable worker's expired-lease path: after three claimed attempts, a restarted worker terminates the job instead of dispatching a fourth provider request. An already completed assessment is preserved and its job is completed without rerunning inference. A still-current lease is not claimed. The same single-iteration method is used by the hosted worker and integration tests; no diagnostic HTTP route was added.
- `JobWorkerLeaseTests.Expired_leases_stop_at_three_attempts_and_preserve_completed_results` and `Exhausted_lease_does_not_erase_an_already_persisted_ai_result` passed against isolated PostgreSQL. Initial fixture setup duplicated the report's automatically created assessment; the tests were corrected to modify that actual queued job/assessment rather than create duplicates. Final full Release suite: **218 passed, 3 opt-in skips, 0 failed**, `backend/SmartCampus.Tests/TestResults/lease-recovery-20261114.trx`. No external provider request was made in these checks. A request interrupted before result persistence can still be uncertain; the attempt cap bounds resubmission and does not promise exactly-once third-party delivery.
- No hosted configuration/data, migration, email transport, commit, push, merge or deployment was changed. Browser acceptance and independent live Gemini retrieval calibration remain outstanding.

## Requirement evidence inventory and maintenance persistence — 14 November 2026 (unreleased)

- Added `docs/REQUIREMENTS_COVERAGE_2026-11-14.md` with all 55 distinct source requirement IDs, source PDF pages, operational route/API/data/authorization map, named test references and explicit remaining acceptance boundaries. The source inventory count and every backend evidence reference were checked against passed entries in the current full-suite TRX. No source draft or diagram review note was promoted into an implemented requirement.
- Strengthened `ApiTests.Maintenance_scope_stale_updates_and_lifecycle_are_enforced`: asserts a real SC reference, unchanged reference at resolution, then actual logout/relogin and persisted resolved status plus four history records. Focused Release test on isolated PostgreSQL passed 1/1, zero skipped/failed; evidence `backend/SmartCampus.Tests/TestResults/maintenance-persistence-20261114.trx`. Production source did not change in this follow-up; the preceding full-suite result remains 218 passed, 3 skips, zero failed.
- No external provider, real recipient email, interactive browser, production data/configuration, migration, commit, push, merge or deployment was used. The matrix distinguishes local scenario evidence from missing institutional data, retrieval-quality calibration, real mail delivery, browser acceptance and hosted verification.

## Stable ordering for operational list pagination — 14 November 2026 (unreleased)

- Maintenance reports, found items, claims and notifications now use `Id` as a deterministic secondary sort after descending `CreatedAt`. This avoids records with identical creation timestamps moving unpredictably between pages for an unchanged dataset. It does not promise a snapshot across concurrent inserts/deletes.
- Added `PaginationTests` coverage for all four endpoints: five synthetic records with equal timestamps, page sizes 1/2/3, no missing or duplicate rows, stable UUID order, owner/requester/recipient visibility, unrelated-account isolation and unauthenticated 401 responses.
- Focused PostgreSQL 16 + pgvector run passed **5/5** (`backend/SmartCampus.Tests/TestResults/pagination-20261114.trx`). Full Release suite passed **223**, skipped **3** opt-in live-provider/gallery cases, failed **0** (`backend/SmartCampus.Tests/TestResults/full-after-pagination-20261114.trx`; 226 total). No API calls were made. The isolated loopback PostgreSQL cluster was cleanly stopped afterward; no hosted or production database was used.
- `git diff --check` passed. No schema/API contract, production configuration, hosted data, commit, push, merge or deployment changed. Browser checks remain deferred under user direction.

## Explicit recovery after terminal Gemini reindex failure — 14 November 2026 (unreleased)

- Code review found that a terminal `unavailable` reindex job kept its idempotency key forever, so missing vectors could not be retried even after the provider or configuration recovered. Automatic key recycling would risk unbounded provider calls, so retries are now explicit: `Search:GeminiReindexGeneration` defaults to `0`; an operator may increment it only after reviewing the terminal failure. The new generation gets a distinct durable key, while repeated scheduler passes remain idempotent and active indexing work still blocks duplicates.
- Every generation remains batch bounded and the job worker's existing three-attempt ceiling still applies. Previously saved text/image vectors are checked and reused, so a new generation requests only missing vectors. This adds configuration support only; no production setting was changed or enabled.
- Added `GeminiReindexerTests.Terminal_reindex_failures_are_retried_only_after_an_explicit_generation_change`: the same generation leaves a terminal failure untouched; incrementing the generation queues one new job; another pass does not duplicate it. Focused reindex tests passed **3/3** (`backend/SmartCampus.Tests/TestResults/gemini-reindex-retry-fixed-20261114.trx`). Full Release suite passed **224**, skipped **3** opt-in provider/gallery tests, failed **0** (`backend/SmartCampus.Tests/TestResults/full-after-reindex-generation-20261114.trx`; 227 total) on isolated PostgreSQL 16 + pgvector. No external API calls were made.
- The isolated PostgreSQL cluster was cleanly stopped. `git diff --check` passed. No migration, hosted environment change, real data, commit, push, merge or deploy occurred.

## Safe offsets for large pagination inputs — 14 November 2026 (unreleased)

- Added a shared page-window calculation that clamps page size to 1–100, normalizes page values below 1, multiplies in 64-bit arithmetic, and caps the EF offset at `Int32.MaxValue`. Applied to maintenance, found items, claims, notifications and admin-account lists. A very large page now returns an empty page instead of overflowing to a negative SQL offset.
- `PaginationTests.Maximum_page_numbers_do_not_overflow_any_operational_offset` checks `Int32.MaxValue` for all five APIs; existing cases still verify deterministic equal-timestamp ordering, complete/disjoint pages, scoping and anonymous rejection. Focused pagination tests passed **6/6** (`backend/SmartCampus.Tests/TestResults/pagination-offset-20261114.trx`). Full Release suite passed **225**, skipped **3** opt-in provider/gallery cases, failed **0** (`backend/SmartCampus.Tests/TestResults/full-after-pagination-overflow-20261114.trx`; 228 total) on isolated PostgreSQL 16 + pgvector.
- `git diff --check` passed and the local PostgreSQL test cluster was cleanly stopped. No external provider/API call, schema change, hosted configuration/data change, commit, push, merge, deploy, or frontend change occurred.

## Shared image normalization for maintenance AI — 14 November 2026 (unreleased)

- Maintenance AI had a separate JPEG conversion path that could reject a valid larger legacy image instead of resizing it. It now uses `CampusImageNormalizer`, shared with uploads and Gemini embeddings, so content validation, 16 MP decode guard, 2048-pixel maximum edge, metadata-free JPEG output and 1.9 MB ceiling are consistent. Invalid content is rejected before inference budget reservation; the provider remains the same pinned free Super VL model.
- Added `ProviderTests.OpenRouter_resizes_and_compresses_legacy_large_image_before_reserving_and_sending`, which sends a generated high-detail image through the application adapter and inspects the actual serialized data URL. The normalized JPEG is within the byte cap and edge limit; the adapter reserves one budget slot and sends one request. `ProviderTests` passed **46/46** (`backend/SmartCampus.Tests/TestResults/provider-image-normalization-fixed-20261114.trx`).
- Full Release suite passed **226**, skipped **3** opt-in provider/gallery tests, failed **0** (`backend/SmartCampus.Tests/TestResults/full-after-ai-image-normalization-20261114.trx`; 229 total) on isolated PostgreSQL 16 + pgvector. No live provider call was made. The test database cluster was cleanly stopped.
- `git diff --check` passed. No schema, host setting, production data, key, migration, commit, push, merge or deployment changed.

## Owner-run Gemini Embedding 2 smoke test and bounded retrieval harness — 14 November 2026 (unreleased)

- The project owner supplied the output of `GeminiLiveTests.Gemini_embedding_2_returns_768_dimensional_text_and_image_vectors_for_synthetic_inputs`: the real Gemini API test passed for one synthetic Arabic text and one generated image, with both vectors asserted at 768 dimensions. The test runner reported 1 passed, 0 failed, 0 skipped and exit code 0, then cleared the test key from its process environment. This confirms the text/image provider integration only; it is not lost-and-found retrieval accuracy, threshold calibration, or evidence that production indexing/search is enabled.
- Added `GeminiRetrievalLiveTests.Saved_lost_found_gallery_runs_through_the_real_application_with_at_most_20_requests` as an opt-in, test-only end-to-end evaluation through the existing admin API. It accepts only an explicitly supplied Gemini test key, an explicit live-test switch, and loopback PostgreSQL whose database ends in `_test`; an outbound HTTP guard allows only POST to the pinned Gemini embedding endpoint with that key and blocks request 21 before transmission. Its fixed gallery and queries are fictional, its preflight requires the configured zero-price/free-tier constraints and 20-call budget, it preserves completed cases, and it does not retry failed provider cases. The test emits per-case expected/actual/ranking/elapsed/error fields without logging keys or raw vectors. `scripts/Run-GeminiRetrievalLiveTest.ps1` prompts with `Read-Host -AsSecureString`, verifies the fixed local `_test` database before asking for the key, and clears process environment values on exit; the script was parser-validated but not run because that would require the test key again.
- The harness built and its focused no-secret run completed with **0 passed, 0 failed, 1 skipped**; the skip was expected because the test key, explicit opt-in, and isolated database were absent. No API request occurred in that run. A live independent retrieval run remains outstanding; hosted search stays closed until relevance and no-match thresholds are independently approved and measured.
- After adding the harness, the full backend Release suite was rerun against isolated PostgreSQL 16 + pgvector and passed **226, skipped 4, failed 0** (`backend/SmartCampus.Tests/TestResults/full-after-gemini-harness-20261114.trx`). The skips were the OpenRouter live test, Gemini text/image live test, bounded Gemini retrieval test, and persisted-gallery replay test; this full run made no external provider requests. The local PostgreSQL test cluster was stopped cleanly afterward.
- No production key/configuration, application database, schema migration, real student data, commit, push, merge or deployment was used or changed. Browser checks remain deferred under the user's direction.

## Automated frontend refresh — 14 November 2026

- `npm test -- --run`: **94 passed in 16 files**. `npm run typecheck`, `npm run lint`, and `npm run build` all passed. The build included the bundled local Nunito Sans, Noto Sans Arabic, and campus image assets.
- This is source/DOM/build evidence only; no browser was opened and no pixel-layout, viewport, Brave, or live application interaction is claimed. No frontend source changed during this verification refresh.

## Actual Gemini retrieval and saved-vector replay — 14 November 2026

- The session-only PowerShell runner executed `GeminiRetrievalLiveTests.Saved_lost_found_gallery_runs_through_the_real_application_with_at_most_20_requests`: **1 passed, 0 failed, 0 skipped**. Exactly **20** real direct Google requests completed with 768-dimensional vectors, one respected minute-budget pause, and no provider error/timeout. Positive query Top-1 and Top-5 were both **6/6**; two positive image queries used the same generated pixels as the gallery and are only identical-image checks. Mean embedding-step time was **617.5 ms**. No general retrieval accuracy is inferred.
- The two absent-item queries had maximum cosine scores **0.690440** (text) and **0.757261** (image). They have no approved independent threshold, so false acceptance is **Not Evaluated**. Combined-query quality, different photographs of the same object, confusable objects, and independent calibration/acceptance remain required. Production search remains closed under its existing gates.
- `CachedLostFoundEvaluationTests.Saved_embedding_gallery_replays_through_application_ranking_without_external_calls` then passed **1/1** with network calls prohibited and unchanged Gemini usage. Removed a stale hardcoded usage total of 21 from that test; it now compares usage before and after replay. A synthetic-only local database backup was saved outside Git before further fixture resets.
- Fixed runner argument binding so PostgreSQL receives all startup options. The owner cancelled persistent encryption; the runner uses `Read-Host -AsSecureString`, process-only environment injection, and cleanup. No DPAPI file or plaintext key file was created. `-Suite Smoke` reuses the same safe session runner for the two-call smoke test.
- Full query rankings, timings and limitations are in [GEMINI_RETRIEVAL_RESULTS_2026-11-14.md](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md). Local ignored evidence: `gemini-retrieval-live-20261114.trx` and `gemini-cached-replay-20261114.trx` under the test results directory.
- Final post-fix Release verification on isolated PostgreSQL 16 + pgvector: **226 passed, 0 failed, 4 opt-in skips**, `backend/SmartCampus.Tests/TestResults/release-final-20261114.trx`. The two provider-dependent Gemini tests were verified separately above. Frontend's current 94/94, typecheck, lint and build results remain applicable; no frontend source changed afterward. Staged-file key-pattern scanning and `git diff --cached --check` passed. Publication is now explicitly authorized by the owner; hosted verification is still pending until the resulting SHA deploys.

## Authorized publication attempt — 14 November 2026

- Created local source commit `39f24ee07043f9b07c96497e8a1b191d4ab578bb` on `codex/release-readiness-20261111`, preserving the prior local commit. Two normal fast-forward push attempts failed before connecting to `github.com:443` (`Could not connect to server`). No force push, merge, branch creation or deployment occurred.
- The connected GitHub API independently confirmed the remote ref still points to `ca84ba95f7c6cd737530413c852fedebd0c7a755`. New-commit CI/deployment verification is therefore blocked by publication connectivity, not claimed successful.
- Read-only live checks returned HTTP 200 for `/` and `/health/ready`. They establish availability of the preceding deployed site only. Authenticated workflows, new-source behavior and independent real-model calibration remain unverified on production.
