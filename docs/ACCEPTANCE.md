# Acceptance evidence — 9 November 2026

This is a local verification report, not a production readiness certificate. PASS means the stated check actually ran. BLOCKED means the indicated external/environment requirement remains unresolved. Historical AI trial measurements are not results from this implementation.

## Current continuation

The tables below retain the earlier local milestone. Current results supersede its test counts: **97 local backend tests passed**, **54 frontend tests passed**, typecheck/lint/build passed, worker offline **6 passed**, backup/verifier guards **11 passed/1 Windows skip**. See [TEST_REPORT.md](TEST_REPORT.md) and `evidence/backend-release-controls.trx` for named evidence.

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
| DASH-01–04: real authorized statistics | PASS for tested counts/scopes | Notification/dashboard count test; maintenance cross-department test. Broader date/recurrence boundary cases remain NOT IMPLEMENTED. |
| UI-01–05/08: preserved design, languages and private gates | PASS for tested subset | Existing frontend suite, actual Arabic/English switching and integrated desktop maintenance journey. Full bilingual coverage of every protected screen remains BLOCKED on browser automation. |
| UI-06/07: responsive/keyboard/accessibility regression | BLOCKED | Existing native browser suite could not launch. Retained base screenshots are historical evidence only. |

## External acceptance gates

- **BLOCKED:** hosted PostgreSQL 17 application connection/read-write verification; runtime storage credentials and real private-bucket policy verification.
- **BLOCKED:** real SMTP verification/reset delivery and university-domain deliverability.
- **BLOCKED:** OpenRouter/NVIDIA calls using configured credentials and reviewed taxonomy; no provider inference was performed in this implementation.
- **BLOCKED:** full SigLIP runtime with declared dependencies and pinned weights. The worker now requires at least 4 GiB free before loading/downloading assets.
- **BLOCKED:** reviewed retrieval calibration and production directory data.
- **BLOCKED:** complete automated browser journeys and deployment/runtime checks.
- **NOT IMPLEMENTED:** production deployment, legacy-data/Auth conversion, comprehensive security penetration/load testing, and automatic reservation timeout. Deployment/conversion/timeouts were not authorized scope additions.

The disk-full incident temporarily stopped PostgreSQL/WSL. The authorized restart initially failed; a later launch recovered it. Final migrations/tests subsequently succeeded. Recursive cleanup of generated bin directories was rejected by automatic review and those directories were retained. Only the task-created SDK archive and an incomplete model download were removed.

The current result is an implemented, locally verified backend/integration with explicit release gates. It is not 100% complete against intended-environment acceptance and is not certified deployment-ready.
