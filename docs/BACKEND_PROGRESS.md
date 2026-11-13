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

## 12 November 2026 — Verified standard-user synthetic AI check (feature branch only)\r\n\r\n- Added a separate verified-user `GET/POST /api/v1/ai/smoke` behind the existing admin smoke flag **and** `AI:SyntheticUserSmokeTestsEnabled` (new default false). Requires `Release:Mode=controlled` and `Release:AllowedEmails`, so public release modes cannot enable it. The endpoint tests only fixed fictional maintenance text, never uploaded files/reports or arbitrary user prompts.\r\n- Server-side atomic Postgres counters cap calls at **one attempt per user per UTC day and three attempts per test deployment per UTC day**, in addition to provider budgets. No migrations or tables were added: counters reuse `InferenceUsage`; a failed external provider attempt still consumes the test slot.\r\n- Student maintenance list displays the button only when the server reports the test enabled; does not reveal any key, data, or AI suggestion. Added backend tests for disabled state, anonymous/CSRF rejection, fixed text, success and repeated attempt. The prior admin smoke route remains restricted to administrators.\r\n- CI run 43124171623 reported a failure in Playwright browser launch timeout; its backend suite passed **157/157**. This change needs fresh CI proof. No production deployment, role grants, live secrets, real AI requests, migrations or student-data transfer occurred.\r\n\r\n## 12 November 2026 — Opt-in admin synthetic AI smoke check (feature branch only)

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
- Local validation: after rebasing over eight newer commits on the same branch, `dotnet test backend/SmartCampus.slnx --configuration Release` passed 188/188 (49 seconds); frontend ESLint, TypeScript, Vitest passed 67/67 and Vite production build passed. `npm audit --audit-level=moderate` found zero vulnerabilities before the rebase; rerun it on the merged head. `dotnet ef database update` succeeded on fresh isolated PostgreSQL 16 + pgvector. The reviewed additive migration is applied to the hosted database; existing table counts matched the pre-migration baseline. Brave rendering/PDF contents, live OpenRouter persisted run, and deployment/CI remain pending and must be verified before completion.
