# Smart Campus

React/TypeScript frontend and ASP.NET Core 10 backend for university maintenance and private lost-and-found workflows. Uses PostgreSQL/pgvector, Identity cookie sessions, private media, department scopes, transactional reservations, independent delivery confirmations, notifications, administration, and AI integrations.

## Project status — 14 November 2026

The live application is hosted on MonsterASP at [smartcampus.tryasp.net](https://smartcampus.tryasp.net/). The `codex/release-readiness-20261111` branch was merged into `main` through [PR #3](https://github.com/MohamedTamer007/smart-campus/pull/3), producing merge commit `f8ef9df`.

Source commit `ae96c51` passed [CI](https://github.com/MohamedTamer007/smart-campus/actions/runs/43538691200) and [automatic deployment](https://github.com/MohamedTamer007/smart-campus/actions/runs/43538684658). These results apply to that source commit; they do not establish fresh test results for the merge commit. Automatic MonsterASP deployment still targets `codex/release-readiness-20261111`. Keep that branch until deployment configuration has been migrated and verified.

**The project is not certified as 100% complete.** Implemented functionality and local scenario tests do not establish full acceptance on the live website.

## Implemented functionality

| Area | Existing implementation | Verification boundary |
|---|---|---|
| Accounts | Registration restricted to the exact `o6u.edu.eg` domain; verification and resend; login/logout; recovery; profile and password management; server-managed sessions | Local workflow, authorization, token-expiry, and session tests; real email receipt still requires acceptance |
| University directory | Departments, hierarchical locations, building/floor selection, and report routing | Tested with synthetic records; approved institutional data is still required |
| Maintenance | Report creation and tracking; state-dependent editing/cancellation; staff review, assignment, transitions, history, and notifications | Local lifecycle, scope, and conflict tests; full live acceptance remains open |
| Maintenance AI | Analysis after review and required approvals; suggestions stored separately from staff decisions; honest failure states | Integration success does not establish classification accuracy or ongoing provider availability |
| Found items and images | Item registration, image, location, custody, private storage, and server-side image validation/resizing/compression | Local upload and authorization tests; production file persistence needs verification |
| Custody | Transfer requests, department-scoped receipt confirmation, cancellation, and non-receipt handling | Authority transfers only after confirmation; local state and reservation tests |
| Recovery claims | Submit, approve, reject, cancel; prevent conflicting reservations; independent confirmations and atomic closure | Local concurrency, authorization, and confirmation tests |
| Search | Text/image embeddings, revision-specific indexing, pgvector ranking, eligible candidates, and scoped viewing grants | Uncalibrated suggestions are available; matching quality and no-match rejection are not approved |
| DSS and administration | Database-backed statistics, filters, maintenance metrics, recurring faults, editable location map, and printing | Local counting, scope, date, and concurrency tests; visual and live acceptance remain open |
| Notifications | Persisted event notifications, read/unread lists, recipient-owned read updates | Local ownership, event, and update tests |
| Frontend | Arabic/English operational pages, RTL/LTR, local fonts, loading/error/empty states, and route protection | DOM and automated checks; comprehensive visual review remains incomplete |

See the [55-requirement coverage matrix](docs/REQUIREMENTS_COVERAGE_2026-11-14.md) for source references, pages, APIs, tables, authorization rules, and named acceptance tests. The matrix is an evidence index, not a completion certificate.

## AI status

| Function | Provider and model | Status |
|---|---|---|
| Maintenance text and image analysis | OpenRouter — `nvidia/nemotron-3.5-super-vl:free` | Integrated with zero maximum pricing and `allow_fallbacks=false`; no automatic paid-model fallback |
| Lost-and-found embeddings | Direct Google API — `gemini-embedding-2`, 768 dimensions | Separate revision from legacy vectors; keys and calls remain on the server |

The [Gemini results report](docs/GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) records a limited real-provider experiment using fictional inputs: **20 completed requests, no provider errors or timeouts in that run, and Top-1 and Top-5 of 6/6 for six positive queries**. Both positive image queries reused gallery images, so they do not establish matching across different photographs. No-match acceptance is **Not Evaluated**. These results are not general accuracy measurements or independent production calibration.

At the owner's request, `Search:AllowUncalibratedCandidates` permits up to five eligible suggestions when approved calibration is unavailable. Responses carry `calibrated: false`, and the interface warns that unrelated items may appear. Similarity does not prove ownership. Privacy, consent, provider, quota, indexing, and authorization gates still apply; this setting alone does not guarantee production search availability. See [suggestion mode and rollback configuration](docs/SEARCH_CANDIDATES_MODE.md).

Gemini usage is restricted to the Free Tier without enabling billing or automatic upgrades. An API key cannot itself enforce free usage: the Google project must remain without billing, and quotas must be monitored. Do not send real student data or images without the required approvals. Never store secrets in React, Git, or logs.

## Recorded verification

- Commit `082fe18`: **226 backend tests passed, 4 skipped, 0 failed**, locally against isolated PostgreSQL. Optional external-provider/evidence tests that were skipped are not counted as successful.
- Commit `ae96c51`: **96 frontend tests passed**, with successful TypeScript checks, ESLint, production build, CI, and deployment linked above.
- Recorded public checks for `082fe18`: `/` and `/health` returned 200; `/api/v1/found` returned 401 without a session; the deployed JavaScript bundle matched the local build.
- Evidence applies to the named commits, excludes the local follow-up changes below, and does not prove every live workflow or manual Brave visual acceptance.

## Remaining work before completion can be approved

1. **Independent retrieval calibration:** Arabic/English descriptions, different photographs of the same item, combined text/image queries, confusable objects, and absent items. Required acceptance: the correct item in the top five for at least 80% of positive cases, with zero false acceptances on no-match cases in the acceptance set. Uncalibrated suggestions do not automatically meet this criterion.
2. **Complete live workflows:** Accounts, maintenance, recovery, custody, both confirmations, notifications, and statistics using authorized test accounts. Verify persistence after refresh, logout/login, and application restart.
3. **Real email acceptance:** Receive verification/recovery messages, exercise valid and expired links, inspect message headers, and verify sender/domain authentication for the configured provider. API acceptance does not establish inbox delivery; DNS changes may require the domain administrator.
4. **Approved university directory:** Complete and validate departments, buildings, floors, and responsibility routing. Synthetic records do not satisfy production requirement LOC-02.
5. **Visual and interaction review:** All pages and important states in Arabic/English at 390, 768, and 1440 pixels, including keyboard navigation, contrast, RTL/LTR, clipping, and overlap. Manual Brave testing is deferred and has not been reported as passed.
6. **Hosting and storage acceptance:** Authorized checks of private images, job/file persistence across idle periods and restarts, and application errors/memory during actual workflows.
7. **Local follow-up changes:** Improved expired-account-link and password-mismatch feedback, plus Gemini usage reporting alongside OpenRouter and associated tests. These remain in the workspace and were not included in PR #3; finish review and verification before publishing them.
8. **After the next deployment:** Confirm CI and deployment for the same SHA, then verify the actual website. A merge alone does not establish deployment success or requirement completion.

## Setup and documentation

- [Windows setup](docs/SETUP_WINDOWS.md)
- [API handoff](docs/API_HANDOFF.md) and [OpenAPI](docs/openapi.json)
- [Architecture](docs/ARCHITECTURE.md)
- [Deployment](docs/DEPLOYMENT.md)
- [Free hosting and Outlook setup](docs/HOSTING_SETUP.md)
- [Progress](docs/BACKEND_PROGRESS.md) and [acceptance evidence](docs/ACCEPTANCE.md)
- [Product requirements](docs/SMART_CAMPUS_REQUIREMENTS.md)

Configure the database and API using the setup guide. Run `npm ci`, then `dotnet run --project backend/SmartCampus.Api --urls http://127.0.0.1:5080` with Development environment enabled. In another terminal run `npm run dev -- --host 127.0.0.1`. Vite proxies API requests; no backend secret belongs in frontend variables. `scripts/publish.ps1` prepares the single-origin application without deployment.

Available frontend checks: `npm test`, `npm run typecheck`, `npm run lint`, `npm run build`, and `npm run test:e2e`. See [Windows setup](docs/SETUP_WINDOWS.md) for backend build/test setup.

PostgreSQL tests require `SMART_CAMPUS_TEST_DB` pointing to an isolated loopback database ending in `_test`; they reset only its application schema. Without it, database tests are skipped. Never point tests at production. Worker boundary tests use a fake model and do not establish inference quality. Search requires completed indexing and provider/privacy configuration; calibrated matching and explicitly enabled uncalibrated suggestions are distinct modes.

See [MonsterASP deployment](docs/MONSTERASP_DEPLOYMENT.md), [hosting configuration](docs/HOSTING_CONFIGURATION.md), and [security review](docs/SECURITY_REVIEW.md). The current full-stack live website is hosted on MonsterASP; historical frontend previews do not establish backend acceptance.
