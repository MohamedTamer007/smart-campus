# Smart Campus

React/TypeScript frontend and ASP.NET Core 10 backend for university maintenance and private lost-and-found workflows. Uses PostgreSQL/pgvector, Identity cookie sessions, private media, department scopes, transactional reservations, independent delivery confirmations, notifications, administration, and AI integrations.

## Current release status — 20 November 2026

**Application-source baseline:** PR #21 merged on 20 November 2026 as `eb3c62c2c743ce792a53f311ce97c3751d422437`, correcting private search feedback and adding release-commit verification after Windows deployment. PR #20 (`7df00ef18ab16e0781a659b15ce806033513f78c`) previously improved image validation and enabled main-push deployment. The last independently verified hosted upload here remains 18 November main commit `b427e37b958d46d9f9348d63791a5e742cf8068f`.

**Latest verified source checks (PR #21):** [CI run 44781998454](https://github.com/MohamedTamer007/smart-campus/actions/runs/44781998454) passed 280 backend tests (four skipped), 140 frontend tests, 51 browser regressions and nine integrated journeys. [Windows package run 44781998433](https://github.com/MohamedTamer007/smart-campus/actions/runs/44781998433) also passed. The new build-info.json check is in main, but a successful post-merge hosted deployment has not been independently verified here.

**Earlier full CI evidence (PR #19):** [PR #19 CI run 44342975370](https://github.com/MohamedTamer007/smart-campus/actions/runs/44342975370) completed successfully on the PR head commit:
- Backend: **280 passed, 4 skipped, 0 failed** against isolated PostgreSQL 17/pgvector.
- Frontend: **130 passed**; typecheck, lint and production build passed.
- Browser automation: **51 existing regression cases and 9 integrated journeys passed**.
- Encrypted database backup/isolated restore, media encryption/restore, backup safety, isolated storage-policy checks and NuGet advisory checks passed.
- Production dependency audit: **0 vulnerabilities**. npm installation still reports **11 high-severity development-dependency advisories**; the CI audit is intentionally limited to production dependencies while the reported picomatch advisory has no patched version in the recorded run.

**Latest hosted deployment evidence:** [MonsterASP deployment run 44348910144](https://github.com/MohamedTamer007/smart-campus/actions/runs/44348910144) was manually dispatched against the same main commit. The Windows publish, WebDeploy upload and post-deployment public /health/live probe succeeded. This confirms a successful upload and API liveness check; it does not prove every authenticated workflow, email delivery, AI provider, storage policy or human acceptance test.

**PR #20 merged:** Browser image decoding/pixel validation, clearer Arabic/English upload-error messages and tests are in main. [CI run 44767019024](https://github.com/MohamedTamer007/smart-campus/actions/runs/44767019024) passed (280 backend, four skipped; 139 frontend; 51 browser regressions; nine integrated journeys), and [Windows package run 44767019137](https://github.com/MohamedTamer007/smart-campus/actions/runs/44767019137) succeeded. Main-push automatic deployment is configured with the `MONSTERASP_AUTO_DEPLOY_DISABLED=true` emergency stop; a post-merge hosted deployment is not independently verified by these pre-merge checks.

**The project is not certified as 100% complete or production-ready.** See [the current release status and open gates](docs/RELEASE_STATUS_2026-11-20.md) and [acceptance evidence](docs/ACCEPTANCE.md).

## Important hosting and privacy boundary

The current public site uses MonsterASP Free. Clause 3.2 of the [provider's Terms of Service](https://www.monsterasp.net/Terms/) restricts Free plans to development, testing, learning and evaluation; it says they are not intended for production workloads or production processing of personal data and are not backed up by MonsterASP. Therefore:

- Use the Free site only with fictional/synthetic test accounts, descriptions, photographs and records.
- Do not collect real student profiles, student emails, credentials, custody details or photographs on that Free host.
- A production university deployment requires an approved paid or university-managed host, an appropriate data-processing agreement, a university-approved legal basis/privacy notice, verified backup/recovery and security review. A paid plan alone does not constitute university approval.

## Implemented functionality

| Area | Existing implementation | Verification boundary |
|---|---|---|
| Accounts | Exact o6u.edu.eg registration domain; email verification/resend; login/logout; recovery; profile/password management; server-managed sessions | Automated local/CI workflows; broad real-user email and acceptance remain open |
| University directory | Departments, hierarchical locations, building/floor selection and report routing | Synthetic records; approved institutional directory and staff scopes are still required |
| Maintenance | Report creation/tracking; state-dependent editing/cancellation; staff review, assignment, transitions, history and notifications | Database-backed lifecycle and authorization tests; human acceptance remains open |
| Maintenance AI | Provider integration, staff-only suggestions, explicit approval gates, non-AI fallback and separation from staff decisions | External inference is disabled by safe defaults; classification quality is not certified |
| Found items and images | Item registration, image normalization/private storage, location, custody and handover | Database and upload tests; hosted storage policy and persistence need separate verification |
| Custody and recovery | Department-scoped receipt confirmation, cancellation, claims, one active reservation and bilateral delivery confirmation | Transactional and authorization tests; full live role-separated acceptance remains open |
| Search | Text/image embeddings, revision-specific indexing, pgvector ranking, candidate grants, keyword fallback and visible similarity components | Suggestion scores are not probabilities; independent retrieval calibration and no-match rejection are not approved |
| DSS and administration | Database-backed statistics, filters, maintenance metrics, recurrence estimates, editable location map and printing | Controlled tests; verified campus data and broader usability/load evaluation remain open |
| Notifications | Persisted event notifications, read/unread lists and recipient-owned read updates | Ownership and event tests |
| Frontend | Arabic/English pages, RTL/LTR, local fonts, loading/error/empty states and route protection | Automated DOM/browser checks; full manual accessibility and visual review remain open |

The [55-requirement coverage matrix](docs/REQUIREMENTS_COVERAGE_2026-11-14.md) is an implementation/evidence index, not a completion certificate.

## AI and search status

The code integrates OpenRouter maintenance analysis, Gemini Embedding 2 retrieval, provider fallback paths, non-AI fallbacks and photo-description drafts. Integration in source is not the same as an enabled hosted feature.

Safe defaults keep external processing off:
- AI:AllowNonSensitiveInference=false
- AI:AllowNonSensitiveImages=false
- AI:AllowItemPhotoDescription=false
- Search:ExternalProcessingApproved=false
- Search:ExternalImagesApproved=false
- Gemini external-processing/image approvals are false and Search:CalibrationApproval is empty.

Do not enable these flags for real student data on the Free host. Any external AI processing requires the correct hosting/privacy approvals and synthetic acceptance first.

The [Gemini retrieval report](docs/GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) records a limited real-provider experiment using fictional inputs: **20 completed requests, no provider errors or timeouts in that run, and Top-1 and Top-5 of 6/6 for six positive queries**. Both positive image queries reused gallery images, so they do not establish matching across different photographs. No-match acceptance is **Not Evaluated**. These results are not general accuracy measurements or independent production calibration.

## Known open issues and release gates

1. **O-03 found-item registration:** the original hosted attempt returned a generic validation message. A later attempt with different fictional inputs succeeded, but the original cause was not established. This branch improves browser-side image decoding and maps known server error codes to actionable Arabic/English messages; that improves diagnosis but does not prove the original failure's cause.
2. **Retrieval quality:** test different photographs of the same object, confusable objects, Arabic/English descriptions and absent items using a reviewed campus dataset. No-match false acceptance remains Not Evaluated.
3. **University data:** approved departments, buildings, floors, staff scopes, retention rules and statistics definitions must be supplied by the university; synthetic records do not satisfy these requirements.
4. **Privacy and hosting:** the Free host must remain synthetic-only. Hosted storage authorization/policy and real backup/recovery must be verified before any real data is processed.
5. **External services:** real email/provider delivery, quota handling and current provider availability need acceptance in the approved environment. AI external-processing flags remain off by default.
6. **Human acceptance:** real users, screen readers, keyboard-only journeys, responsive visual review and larger-scale load tests have not been completed.
7. **Dependencies:** 11 high-severity development-dependency advisories were reported by npm installation; production audit returned zero vulnerabilities. Reassess when a patched dependency is available; do not use npm audit fix --force without compatibility testing.

## Setup and checks

- [Windows setup](docs/SETUP_WINDOWS.md)
- [API handoff](docs/API_HANDOFF.md) and [OpenAPI](docs/openapi.json)
- [Architecture](docs/ARCHITECTURE.md)
- [Deployment record](docs/DEPLOYMENT.md) and [MonsterASP deployment](docs/MONSTERASP_DEPLOYMENT.md)
- [Hosting configuration](docs/HOSTING_CONFIGURATION.md)
- [Progress](docs/BACKEND_PROGRESS.md), [acceptance evidence](docs/ACCEPTANCE.md) and [security review](docs/SECURITY_REVIEW.md)

Run npm ci, then dotnet run --project backend/SmartCampus.Api --urls http://127.0.0.1:5080 with Development environment enabled. In another terminal run npm run dev -- --host 127.0.0.1. Vite proxies API requests; no backend secret belongs in frontend variables. scripts/publish.ps1 prepares the single-origin application.

Available frontend checks: npm test, npm run typecheck, npm run lint, npm run build, and npm run test:e2e. PostgreSQL tests require SMART_CAMPUS_TEST_DB pointing to an isolated loopback database ending in _test; they reset only its application schema. Never point tests at production. Worker boundary tests use a fake model and do not establish inference quality.
