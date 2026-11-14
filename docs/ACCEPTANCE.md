# Acceptance evidence — current local continuation 14 November 2026

This is a local verification report, not a production readiness certificate. PASS means the stated check actually ran. BLOCKED means the indicated external/environment requirement remains unresolved. Historical AI trial measurements are not results from this implementation.

## Current continuation

Real Gemini retrieval follow-up: 20/20 provider requests completed, six positive queries had Top-1/Top-5 6/6 (including two identical-image checks), and two no-match queries remain uncalibrated. A network-prohibited replay passed with unchanged provider usage. See [GEMINI_RETRIEVAL_RESULTS_2026-11-14.md](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md). The final isolated backend suite passed 226 with 4 opt-in skips and zero failures (`release-final-20261114.trx`). These results do not authorize production search activation.

Per-ID coverage, implementation locations and explicit open acceptance gates for all 55 source requirements: [REQUIREMENTS_COVERAGE_2026-11-14.md](REQUIREMENTS_COVERAGE_2026-11-14.md).

The tables below retain earlier local milestones. Latest source evidence: backend Release suite **226 passed, 0 failed, 4 skipped** on isolated PostgreSQL 16 + pgvector (`backend/SmartCampus.Tests/TestResults/full-after-gemini-harness-20261114.trx`). Skips are the opt-in OpenRouter and Gemini live requests, bounded Gemini retrieval evaluation, and saved-gallery replay. The suite made no external provider calls. New tests cover deterministic equal-timestamp pagination, safe maximum page offsets across five APIs, explicit bounded retry of terminal Gemini reindex jobs, normalization of a large maintenance photo before provider budget reservation, and the 20-request maximum in the retrieval harness. Frontend suite **94/94**, `typecheck`, ESLint and production build passed in the current continuation. The owner-run session-only Gemini test separately passed with one synthetic Arabic text and one generated image, both 768-dimensional. This proves integration, not ranking quality. Image-only and combined application queries were verified against PostgreSQL with synthetic encoder vectors; real-model retrieval remains uncalibrated. Browser/visual acceptance, real email delivery, verified campus directory, production privacy approval and hosted verification remain open. See [BACKEND_PROGRESS.md](BACKEND_PROGRESS.md) for current named evidence and the dated sections below for historical results.

GitHub CI has established PostgreSQL 17 test compatibility, a real encrypted application dump/isolated restore with all 26 application tables/48 rows, constraints/indexes/history compared, a clean full-history Gitleaks scan with two reviewed exact false-positive fingerprints, isolated storage-policy proof, media encryption/restore roundtrip, and actual pinned-model CPU inference (768-dimensional vectors). CI run 42693231390 passed all 36 static and nine integrated browser cases across three engines, API image build and Caddy configuration validation. The complete production API/worker deployment remains unverified. That CI run tested the preceding committed source (92 backend cases); current Resend evidence changes passed 97 local backend cases.

**BLOCKED:** full public deployment, platform HTTPS hostname for the complete worker-capable host, hosting account access, authorized real email transport, hosted least-privilege runtime setup, storage-prefix permission correction, real provider calls, verified directory/taxonomy, calibration, complete off-host recovery and load targets. **NOT IMPLEMENTED:** Microsoft Graph email adapter, full operational alerting and exhaustive browser journeys. Free hosting comparison/account-owner instructions are in [HOSTING_SETUP.md](HOSTING_SETUP.md).

## Earlier executed checks

| Check | Result | Evidence |
|---|---|---|
| Backend build, including Release API | PASS | Zero warnings/errors in final Release build. |
| Backend tests | PASS | 36 passed, 0 failed, 0 skipped; `evidence/backend-final.trx`. Includes real PostgreSQL 16/pgvector persistence and concurrency. |
| Frontend tests | PASS | 43 passed in eight files; `npm test`. |
| Typecheck/lint/frontend build | PASS | `npm run typecheck`, `npm run lint`, `npm run build`. Generated wwwroot/bin/obj are excluded from frontend lint. |
| Worker offline boundary tests | PASS | Six unittest tests; a labelled fake model boundary is used. No real pretrained inference is claimed. |
| npm/NuGet advisory checks | PASS | npm audit: zero vulnerabilities; NuGet reported no vulnerable direct/transitive packages. |
| Worker dependency metadata | PASS, limited | Current PyPI metadata lists no advisories for declared transformers 5.22.2, torch 2.15.0 and Pillow 13.0.0. Full inference environment/transitive audit remains blocked; the shared test environment still has torch 2.11.0. |
| Local publish / secret exclusions | PASS | Release publish completed; appsettings.Local.json and demo credentials were absent. |
| EF migrations | PASS locally | Applied to development database and freshly applied by tests; legacy hosted tables were not changed. Idempotent review SQL is in database.sql. |
| Actual browser/API/database communication | PASS, stated journey | In-app browser: university-account demo login, dependent locations, report creation/tracking, logout, scoped staff login, review/assignment, repair and resolution. Screenshot: screenshots/backend-maintenance-resolved.png. |
| Native Playwright automation | BLOCKED | All 12 existing tests failed at browser launch, not application assertions. Cached Chromium returns Windows bad-image/spawn failure. Two new integrated specs are discoverable but not executed. |
| Docker/Linux runtime | BLOCKED | Docker unavailable; Windows Release compilation is not a container execution result. |

## Requirement coverage

| Requirement | Status | Evidence |
|---|---|---|
| AUTH-01–03: exact university domain, registration, verification, login, recovery | PASS locally | `Deceptive_email_is_rejected`, `University_email_is_normalized`, `Registration_verification_recovery_and_replayed_tokens_follow_real_flows`; local mail sink. |
| AUTH-04: malformed/replayed account actions | PASS for tested cases | `Malformed_account_action_tokens_return_validation_errors`, `Expired_verification_and_reset_tokens_are_rejected`, registration/recovery test and frontend form-validation tests. Expiration is exercised using token-policy overrides. |
| AUTH-05: session revocation/expiry and CSRF | PASS | `Missing_csrf_is_rejected_and_logout_revokes_session`, `Expired_database_session_is_rejected_even_with_a_cookie`, `refreshes CSRF after login before changing account data`. |
| Privileges and account administration | PASS | `Unauthenticated_unverified_and_forged_privileges_are_denied`, `Admin_grants_are_versioned_and_revoke_existing_sessions`. |
| LOC-01/03: required hierarchy and dependent dropdowns | PASS with synthetic fixtures | `Invalid_upload_and_inconsistent_location_are_rejected`; actual browser selected real stored DEMO ONLY faculty/building/floor rows. |
| LOC-02: verified production directory | BLOCKED | Team must provide real faculties/buildings/floors/departments/routing. Demo rows are explicitly synthetic. |
| LOC-04: unavailable data | PASS for existing UI tests | No false default locations; frontend validation and empty/failure components exercised. |
| MAINT-01–05/07: reports, references, permissions, transitions, history, cancellation | PASS | `Maintenance_scope_stale_updates_and_lifecycle_are_enforced`, `Prehandling_cancellation_and_rejection_preserve_truthful_states`; completed manual browser journey. |
| MAINT-06/08: staff-only suggestions, validated output, failure states | PASS for authorization/contracts | Maintenance scope test checks ordinary response has no assessment; `Valid_AI_output_preserves_priority_and_risk_separately`, `Unrecognized_or_truncated_AI_output_is_rejected`, `Provider_failure_never_creates_a_suggestion`. Real inference BLOCKED. |
| FOUND-01–04: required image, custody identity and privacy | PASS locally | `Invalid_upload_and_inconsistent_location_are_rejected`, `Private_inventory_media_and_uncalibrated_search_do_not_leak`, custody transfer test. |
| CUST-01–04: actual receipt, department scope, non-receipt and cancellation | PASS | `Custody_changes_only_on_scoped_receipt_and_former_custodian_loses_authority`, `Pending_handover_cancellation_keeps_item_and_original_custody`, reservation guard test. |
| SEARCH-01–06: gating, private result access and SQL retrieval | PASS for tested contracts | `Private_inventory_media_and_uncalibrated_search_do_not_leak`, `Pgvector_ranking_filters_candidates_and_persists_user_specific_result_grants`. Retrieval test uses synthetic vectors and test-only calibration, not a real model or quality benchmark. |
| Real visual similarity / qualifying-match calibration | BLOCKED | Full weights could not download safely; reviewed positive/no-match cases and release threshold are not supplied. General search stays disabled. |
| CLAIM-01–06: request eligibility, one reservation, privacy, duplicate/conflict handling | PASS | `Concurrent_approvals_create_one_reservation_and_both_confirmations_close_it`, `Reservation_blocks_transfer_and_cancellation_releases_existing_claims`, private inventory test. |
| DEL-01–04: independent confirmations, both orders, repeat safety and closure | PASS | Concurrent-approval test and `Delivery_first_still_requires_independent_requester_receipt`. |
| NOTIF-01/02: recipient inbox, read state, related-record enforcement | PASS for API | `Notifications_are_recipient_owned_and_dashboard_counts_use_real_records`; related routes separately enforce authorization. |
| DASH-01–04: real authorized statistics | PASS for tested counts/scopes and Cairo date boundaries | `Administrator_operational_summary_contains_real_counts_without_private_payloads`, `Admin_statistics_return_named_buildings_workload_metrics_and_save_map_positions_with_version_checks`, `Workload_metrics_count_only_non_cancelled_reports_and_average_valid_resolution_times`, `New_claim_on_old_item_is_counted_by_claim_date_and_department`, and `Date_filter_includes_cairo_day_boundaries_and_excludes_the_following_midnight`. |
| UI-01–05/08: preserved design, languages and private gates | PASS for tested subset | Existing frontend suite, actual Arabic/English switching and integrated desktop maintenance journey. Full bilingual coverage of every protected screen remains BLOCKED on browser automation. |
| UI-06/07: responsive/keyboard/accessibility regression | BLOCKED | Existing native browser suite could not launch. Retained base screenshots are historical evidence only. |

## External acceptance gates

- **BLOCKED:** hosted PostgreSQL 17 application connection/read-write verification; runtime storage credentials and real private-bucket policy verification.
- **BLOCKED:** real SMTP verification/reset delivery and university-domain deliverability.
- **PARTIALLY VERIFIED:** bounded fictional OpenRouter smoke tests using the separate free-tier test key completed in two runs: 40 successful model requests in the earlier campaign and 6 on the replacement key. They do not validate durable authenticated workflows or classification accuracy. See the newest application-path entry below; no API calls were made in the threshold implementation check.
- **BLOCKED:** independent retrieval calibration and acceptance. The chosen hosted OpenRouter embedding path is the current target; historical SigLIP worker startup is not evidence for that hosted model's accuracy and is not a substitute for its independent calibration.
- **BLOCKED:** reviewed retrieval calibration and production directory data.
- **BLOCKED:** complete automated browser journeys and deployment/runtime checks.
- **NOT IMPLEMENTED:** production deployment, legacy-data/Auth conversion, comprehensive security penetration/load testing, and automatic reservation timeout. Deployment/conversion/timeouts were not authorized scope additions.

The disk-full incident temporarily stopped PostgreSQL/WSL. The authorized restart initially failed; a later launch recovered it. Final migrations/tests subsequently succeeded. Recursive cleanup of generated bin directories was rejected by automatic review and those directories were retained. Only the task-created SDK archive and an incomplete model download were removed.

The current result is an implemented, locally verified backend/integration with explicit release gates. It is not 100% complete against intended-environment acceptance and is not certified deployment-ready.

## Local source continuation — 13 November 2026

- Backend: `dotnet test backend/SmartCampus.slnx --configuration Release` — **185 passed, 0 failed, 0 skipped** against a fresh temporary PostgreSQL 16 + pgvector database in WSL. The temporary database and role were removed after the run.
- Frontend: `npm test -- --run` — **72 passed**; `npm run typecheck`, `npm run lint`, and `npm run build` passed. `git diff --check` passed.
- Added active/confirmed-account filtering for departmental handover and custodian notifications, with a database integration regression. Added DSS date-boundary and recurrence regression across Cairo daylight-saving time.
- This continuation did not use Brave, start the local application, call OpenRouter, send email, or touch hosted records/settings. Interactive visual inspection, real provider inference, authenticated hosted user workflows, real mail delivery, verified production campus locations and search calibration remain unverified. The 55 project requirements therefore are not declared fully accepted.
- All changes remain uncommitted on `codex/release-readiness-20261111`; no push, merge, or deploy occurred.

## Latest backend and live-provider evidence — 13 November 2026

- Backend Release suite passed **187/187** on an isolated temporary PostgreSQL 16 + pgvector database.
- A dedicated free-tier OpenRouter test key (separate from production) was used only in memory after DPAPI decryption. Text classification produced valid allowed JSON (`plumbing/high/high`, 698 ms) when matching the application's disabled-reasoning payload. A generated pipe image also produced valid JSON (`plumbing/high/high`, 1,111 ms). Two earlier text probes returned incomplete content; a non-maintenance bottle illustration returned an irrelevant `safety/high/high` classification and was excluded from evaluation.
- Four synthetic embedding requests with `data_collection=deny` returned HTTP 404. A one-shot fictional text-passage request with `data_collection=allow` returned HTTP 200 and a valid normalized 1024-dimensional vector in 1,082 ms. This points to the routing privacy policy, rather than the 1024 dimension, as the likely 404 cause. Image embeddings, candidate ranking and retrieval accuracy remain unverified; search stays closed. OpenRouter says `deny` excludes data-collecting providers, and this model's only provider publishes a logging/data-improvement notice. Any production use therefore requires explicit governance approval of that collection policy. A source gate now fails closed without explicit `Search:ProviderDataCollection=allow`; its default is still deny.
- All requests explicitly pinned `:free`, `max_price=0`, `allow_fallbacks=false`, with no retry; only fictional text and generated illustrations were sent. The API key endpoint identified the key as free-tier. The encrypted test-key file remains outside the repository at `%LOCALAPPDATA%\SmartCampusTests\openrouter.key.dpapi` because the automatic review rejected its deletion; generated synthetic fixtures also remain in the OS temp folder. The key was not printed or committed. Production key/settings were untouched.
- Brave visual acceptance, authenticated live workflows, production mail/directory checks, and 55-requirement completion remain unverified. No commit, push, merge, or deployment occurred.

## Privacy-route gate follow-up — 13 November 2026

- Synthetic text-passage and image-passage embeddings with `data_collection=allow` each returned HTTP 200 and normalized 1024-dimensional vectors (1,082 ms and 1,075 ms). Four earlier requests with `deny` returned 404. The implementation now refuses to call the embedding provider unless this policy is explicitly set to `allow`, and still requires separate external-processing consent. Defaults and production settings remain deny/unchanged. This is a governance approval requirement because NVIDIA's free endpoint may collect/log inputs. A separate 2-candidate retrieval composition attempt stopped on its first HTTP 400; no retries occurred.

## Test-key follow-up — 13 November 2026

- The separately stored DPAPI-protected test key decrypted only in process memory. One fictional embedding request pinned to `nvidia/nemotron-3.5-embed-8b:free`, `max_price=0`, and `allow_fallbacks=false` received HTTP **401**. The request was not retried; it produced no embedding and no ranking. This indicates the saved key is not currently accepted by OpenRouter. No production key was read or changed.
- This attempted request had a zero-price model configuration; no successful model inference occurred. OpenRouter documents that failed free-model attempts still count toward the free daily request cap (50/day, 20/minute); exact remaining quota was not available, so no additional live AI requests were sent. See [OpenRouter free model limits](https://openrouter.ai/collections/free-models/).
- The test-key file remains encrypted and outside the repository. Retrieval Top-1/Top-5, no-match false positives, and threshold calibration remain unverified; search remains closed. No browser, hosted application, user data, commit, push, merge, or deployment was used.

## Multi-role navigation — 13 November 2026

- Primary navigation now recognizes additional granted roles for the staff work queue and admin link, and staff with a secretariat role can switch between their maintenance and custody queues. The frontend suite passes **75/75**, including positive multi-role, secretariat custody-switching, and ordinary-user navigation cases; typecheck, lint, and production build pass. This is code/test evidence only, not a browser or access-control test; backend authorization tests remain the authority for protected data/actions.

## Maintenance provider data-collection gate — 13 November 2026

- The free Super VL model's OpenRouter page discloses logging for security and product improvement and warns against confidential or personal inputs. Maintenance inference now also requires an explicit `AI:ProviderDataCollection=allow` setting, in addition to the existing external inference and staff-review gates. Its default remains `deny`; the fixed free model, zero price caps and disabled provider fallbacks remain in each request. The admin operations response and UI report the non-secret policy state. No production environment setting was changed.
- `ProviderTests.Unapproved_provider_data_collection_blocks_maintenance_inference_before_budget_or_http` proves both `deny` and missing policy block before budget reservation or HTTP. `ProviderTests.OpenRouter_text_classification_uses_pinned_free_model_without_paid_fallback` verifies the allow policy is sent with the free/zero-price/no-fallback route. `ApiTests.Administrator_operational_summary_contains_real_counts_without_private_payloads` now asserts that the default reports the collection policy as unapproved; this database-backed test was compiled but skipped in this run because `SMART_CAMPUS_TEST_DB` is absent. Frontend tests verify both approved and denied operations-state copy.
- Latest verification after the gate: focused `ProviderTests` **26/26**; backend full suite **136 passed, 55 skipped**; frontend **77/77**; typecheck, ESLint, build and dependency audits passed. No live provider request was made for this change. The current saved test key's earlier HTTP 401 and retrieval-calibration blocker are recorded above.
- `RemoteEmbeddingsTests` 15/15, `ProviderTests` 43/43, complete PostgreSQL 16 + pgvector backend suite 189/189, `git diff --check` passed. The isolated test DB lacked pgvector on the first attempt; after installing the extension only in that `_test` database, all 189 tests passed.
- Frontend regression suite 72/72; TypeScript typecheck, ESLint and Vite production build passed. `npm audit --omit=dev` reported 0 vulnerabilities; NuGet audit reported no vulnerable packages.
- The automatic review rejected cleanup of the isolated test DB and role; the temporary role was reduced to `NOLOGIN NOSUPERUSER`. The `_test` DB and temp connection file remain local, isolated, and outside the hosted environment. No production data/settings, push, merge, or deploy were touched.
- This does not establish retrieval accuracy or enable search. Text/image embedding works in isolated synthetic probes, but search ranking, Top-1/Top-5 acceptance, no-match false positives and calibration remain unverified. Browser and complete project acceptance remain unverified.

## Additional-role authorization presentation — 13 November 2026

- Shared role recognition now drives route access, navigation, admin schematic controls, and privileged found-item custody details. Added regressions for an account whose primary role is user but holds an additional admin grant, including admin map controls and private custody data; ordinary-user privacy coverage remains.
- Frontend verification after these changes: **79/79** tests, TypeScript typecheck, ESLint, and production build all passed. No browser was used, per instruction; visual acceptance remains deferred.
## Full isolated backend re-run — 13 November 2026

- Created a fresh PostgreSQL 16 cluster under `/tmp/smartcampus_codex_pg_20261113`, bound the server to `127.0.0.1:55439`, and created only the isolated `smartcampus_codex_20261113_test` database. Enabled pgvector within that database. The cluster was stopped after verification; no development or hosted database was used.
- First full run: 186 passed, 5 failed. The failures all came from the synthetic maintenance integration fixture missing the newly required explicit provider data-collection approval, so the intended worker behavior was blocked before reaching its test boundary.
- Fixed only the test fixture to set `AI:ProviderDataCollection=allow` alongside its existing synthetic-only provider key and fake HTTP boundary; production and default settings remain `deny`. Re-ran `dotnet test backend/SmartCampus.slnx --configuration Release`: **191 passed, 0 failed, 0 skipped** in 1m34s.
- This verifies database-backed code and mocked provider boundaries, not live OpenRouter inference, model accuracy, retrieval ranking, hosted workflows, email delivery, or browser rendering.
## Custody role compatibility — 13 November 2026

- Aligned the frontend custody-summary predicates with the shared role helper, so a legacy/additional secretariat grant is not discarded merely because a roles array is present. Existing department-scoped custody behavior is unchanged; the backend remains the authority for access.
- Added an Arabic/English-independent regression for incoming and held items and role scopes. Frontend verification after the change: **80/80** tests; TypeScript typecheck, ESLint and production build passed. No browser was used.
## Current OpenRouter synthetic smoke — 13 November 2026

- The saved test key is PowerShell `ConvertFrom-SecureString` DPAPI text, so it was read with `ConvertTo-SecureString` and materialized only in process memory. `/api/v1/key` accepted it and reported `is_free_tier=true`; no key or credential-bearing response was printed. Its metadata did not disclose remaining free-request quota.
- Six model requests were sent sequentially, with no retries, only synthetic fictional content. Every payload pinned the appropriate `:free` model, set provider `max_price.prompt=0` and `completion=0`, and `allow_fallbacks=false`. Provider `data_collection=allow` was limited to these fictional fixtures. No production key or setting was used.
- Maintenance text: **HTTP 200, 940 ms**, valid allowed JSON `plumbing/high/high` for a fictional Egyptian-Arabic leak near an electrical outlet.
- Maintenance image: **HTTP 200, 2,018 ms**, valid allowed JSON `plumbing/high/medium` for a generated pipe-leak illustration. This is one response, not accuracy evidence.
- Lost & Found embeddings: text passage **629 ms**, text query **2,576 ms**, image passage **1,430 ms**, image query **2,512 ms**; each returned a normalized 1,024-dimensional vector from the requested free model (the response canonicalized away the `:free` suffix). Synthetic pair cosine was **0.64071** for related text and **0.98444** for two drawings of the same fictional umbrella.
- The synthetic retrieval check had one candidate per pair. It does not measure Top-1/Top-5, threshold quality, or false accepts on no-match cases. Search remains closed; a reviewed independent positive/no-match set and calibration are still required. The requests were direct API smoke calls, not persisted through the app's durable worker or authenticated workflow.
## Application-code OpenRouter live test — 13 November 2026

- Added `OpenRouterLiveTests.Current_application_provider_paths_accept_only_pinned_free_synthetic_calls`, opt-in only when both `SMART_CAMPUS_RUN_LIVE_AI_TESTS=true` and `SMART_CAMPUS_OPENROUTER_TEST_KEY` are set. It uses the actual `AiProviders` and `RemoteEmbeddings` classes, a six-reservation hard cap, fake fictional report/item inputs, and an in-memory client with no retry. With the opt-in variables absent it is skipped and makes no network call.
- Ran the focused test with the separately stored test key loaded into process memory only: **1/1 passed**, 6 actual model requests. Maintenance text returned `plumbing/high/high` (1,703 ms); maintenance image returned `plumbing/medium/medium` (8,369 ms). Four text/image embedding passage/query calls each returned 1,024-dimensional vectors. The outputs were allowlist-valid; no accuracy reference was asserted.
- Immediately ran the complete database-backed Release suite without the live-test opt-in: **191 passed, 1 skipped, 0 failed**. This confirms ordinary suite execution does not spend provider quota. The skip is intentional and documented.

## Separate retrieval score thresholds — 13 November 2026 (unreleased)

- Exploratory synthetic retrieval (not an independent acceptance set): text queries retrieved the intended item at Top-1/Top-5 for 4/4 positive cases, with maximum score 0.32874 for 2 no-match queries; image queries retrieved the intended item at Top-1/Top-5 for 4/4 positive cases, with maximum score 0.72054 for 2 no-match queries. Minimum positive scores were 0.43035 for text and 0.97032 for image. These tiny results demonstrate different score scales only; they are not accuracy, calibration, or release evidence.
- Hosted retrieval now reads `Search:TextMinimumScore` for text-only search and `Search:MultimodalMinimumScore` for text-plus-image search. Each requires the current embedding revision and a non-empty approval record. The admin readiness flag is true only when both are valid. The old `Search:MinimumScore` remains supported only for the legacy local worker. Defaults/examples keep hosted thresholds empty; search remains closed.
- `RuleTests.Hosted_search_requires_independently_approved_thresholds_for_both_modalities` and `RuleTests.Hosted_search_rejects_non_finite_or_out_of_range_modality_thresholds` pass. Focused `RuleTests`: **24/24**. Full isolated PostgreSQL 16 + pgvector backend Release run: **200 passed, 1 intentional opt-in live-provider skip, 0 failed**. The temporary cluster was stopped after the run. No provider calls were made in this validation.
- No frontend, browser, hosted workflow, or production configuration was changed or tested for this threshold adjustment. The 55 requirements are not declared complete; independent retrieval calibration, privacy/governance approval, live authenticated workflows, email delivery and visual inspection remain open.

## Revalidated separate OpenRouter key — 13 November 2026

- The encrypted external test-key file was replaced through the secure local prompt; the plaintext value was not sent in chat or printed. `OpenRouterLiveTests.Current_application_provider_paths_accept_only_pinned_free_synthetic_calls` passed **1/1** using 6 model requests: maintenance text JSON classification, generated maintenance image JSON classification, text passage/query embeddings, and generated found-item/query image embeddings. Responses were allowlist-valid and embeddings were 1,024-dimensional; this is integration evidence, not accuracy evidence.
- All six requests used pinned `:free` IDs, zero prompt/completion price caps, disabled provider fallbacks and no retries. Inputs were fictional. No production key/settings or database were used. The key was cleared from process environment after the test.
- Across the two separate key runs in this thread, 46 successful model requests were observed (40 earlier and 6 on the replacement key). OpenRouter did not expose remaining quota per key. No further provider request is made as part of this continuation.

## DSS workload metric regression — 13 November 2026

- Added `StatisticsMetricRegressionTests.Workload_metrics_count_only_non_cancelled_reports_and_average_valid_resolution_times`, using only local synthetic PostgreSQL rows. It verifies cancelled reports remain in total/status counts but are excluded from the resolution-rate denominator; two resolutions average 60 hours; open workload, oldest-open age, staff-confirmed recurrence and eight-week trend are returned consistently.
- Focused integration test passed 1/1. The complete isolated PostgreSQL 16 + pgvector Release suite then passed **201 passed, 1 intentional live-provider skip, 0 failed**. The temporary database cluster was stopped after testing. No OpenRouter calls or browser testing occurred in this DSS check.

## Current continuation verification — 13 November 2026 (unreleased)

- Test-key metadata accepted the locally DPAPI-protected key and reported `is_free_tier=true`; its response does not reveal remaining daily `:free` requests. OpenRouter documents a maximum of 50 requests/day and 20/minute for free variants ([limits](https://openrouter.ai/collections/free-models/)).
- Focused live test `dotnet test backend/SmartCampus.slnx --configuration Release --filter 'FullyQualifiedName~OpenRouterLiveTests' --logger 'console;verbosity=detailed'` passed **1/1**, with six actual sequential fictional requests and no retries. Maintenance text returned `plumbing/high/high` (8,367 ms); generated maintenance image returned `plumbing/medium/medium` (11,039 ms). Text passage/query embeddings took 11,115/17,005 ms; generated image passage/query embeddings took 24,932/24,706 ms. All four vectors were 1,024-dimensional. Requests used only pinned `:free` models, zero price caps, and disabled provider fallbacks. This checks API integration/response shape only, not accuracy or retrieval rank quality.
- Frontend: after localizing known maintenance categories/severity values in AI suggestions and DSS fault summaries, and removing unused synthetic AI smoke calls from the frontend service, `npm test -- --run` passed **82/82**; `npm run typecheck`, `npm run lint`, and `npm run build` passed. The existing UI test now asserts the readable English label instead of the raw model token.
- `npm run test:e2e` discovered 42 cases but **0 executed**: Chromium, Firefox, and WebKit launch failed because their browser executables are absent from the environment. These are launch failures before app assertions, not application test failures. No browser binaries were installed; Brave visual testing remains deferred.
- No production key/configuration, hosted data, email, commit, push, merge, or deployment was accessed or changed. Retrieval calibration, production privacy approval, verified directory data, authenticated live workflows, real email delivery, and visual/browser acceptance remain open; completion is not claimed.

## DSS priority and risk distributions — 13 November 2026 (unreleased)

- `/api/v1/statistics` now returns priority and risk distributions from the staff decision columns on reports, within the same date/department authorization scope. Cancelled reports are excluded. Separate AI assessment records are never included.
- Admin overview now shows responsive priority and risk distributions, translated for Arabic and English, with copy stating they represent staff decisions. Existing API clients remain compatible because these response properties are additive.
- `StatisticsTests.Statistics_expose_only_staff_confirmed_priority_and_risk_decisions` passed 1/1 against isolated PostgreSQL 16 + pgvector. It verifies AI-only assessment values do not inflate the staff distributions. Full backend Release suite passed **202, 1 intentional live-provider skip, 0 failed**. Frontend **82/82**, TypeScript, ESLint and build passed. The temporary PostgreSQL cluster was stopped.

## Gemini Embedding 2 migration — local/unreleased — 13 November 2026

- Backend Release suite: **211 passed, 3 skipped, 0 failed** against isolated loopback PostgreSQL 16 + pgvector. Skips are opt-in live provider/evaluation tests; no Gemini API request was made by this suite. Focused reindex tests passed **2/2**, covering opt-in/bounds/idempotency, resuming missing image vectors only with image approvals, and keeping that work off without image consent.
- Frontend: **82/82** Vitest tests passed; `npm run typecheck`, `npm run lint`, and `npm run build` passed. The admin-map test no longer emits duplicate-key warnings after deduplicating its selector options.
- `RemoteEmbeddingsTests` cover Gemini's pinned endpoint/model, 768-dimension response, secret header placement, Arabic/English text prompts, image normalization, cache, error sanitization and no retries. These are local boundary tests; they do not prove Google accepted a request.
- Real Gemini requests were deferred at the user's request; no API call was made. A synthetic DPAPI round-trip passed separately, but no test key remains stored in the local test environment.
- No production environment variable, data, migration, deployment, commit, push, or merge was changed. AI approvals remain false; Nemotron Super VL configuration remains unchanged. Search remains fail-closed pending real synthetic API confirmation and separate retrieval calibration.
