# Deployment continuation test report

This report separates current observed results from earlier implementation evidence in [ACCEPTANCE.md](ACCEPTANCE.md).

## Current verified batch

Full local run: **90 passed, zero failed/skipped**, `dotnet test backend/SmartCampus.slnx --no-build --logger 'trx;LogFileName=backend-release-controls.trx' --results-directory ../../work/test-results`, isolated PostgreSQL 16/pgvector. Fresh test fixtures apply all seven EF migrations. Selected report: `evidence/backend-release-controls.trx`. The same source compiled successfully in the preceding targeted run; `--no-build` reused that assembly.

| Requirement | Evidence | Result |
|---|---|---|
| Quota concurrency cannot exceed daily allowance | `Concurrent_inference_reservations_never_exceed_daily_budget` | PASS |
| Minute rejection does not spend day budget | `Minute_limit_does_not_spend_daily_budget_and_resumes_next_window` | PASS |
| No implicit NVIDIA allowance | `Unconfigured_nvidia_budget_blocks_inference_without_counter_rows` | PASS |
| UTC day reset | `Daily_budget_resets_only_at_UTC_boundary` | PASS |
| Quota failure blocks provider contact | `Exhausted_budget_prevents_external_inference` | PASS, explicit HTTP boundary |
| Operational diagnostics restrict private information | `Operational_diagnostics_reject_guests_users_and_scoped_staff`, `Administrator_operational_summary_contains_real_counts_without_private_payloads` | PASS |
| Controlled release blocks unlisted access | `Controlled_release_blocks_unlisted_registration_and_login`, `Removing_testing_authorization_blocks_existing_session_but_preserves_logout` | PASS |
| Controlled recovery does not enumerate/send | `Controlled_release_keeps_unlisted_recovery_and_resend_generic_without_sending` | PASS |
| Authorized registration still requires verification | `Authorized_testing_account_can_register_and_still_requires_verification` | PASS, local mail sink only |
| Unreviewed report never reaches provider | `Unreviewed_report_never_reaches_external_provider` | PASS |
| Scoped, explicit, current-version input approval | `Ai_review_requires_current_scoped_staff_explicit_confirmation_and_version` | PASS |
| Suggestions stay private/separate from decisions | `Reviewed_analysis_stays_private_and_never_sets_staff_decision` | PASS |
| Image approval binds the exact sanitized attachment | `Image_is_forwarded_only_after_explicit_approval_of_the_attached_image` | PASS, synthetic image/network boundary |
| Review cannot race a running job | `Review_cannot_requeue_a_job_while_a_worker_holds_its_lease` | PASS |
| Edits invalidate approval and late output | `Edited_report_invalidates_approval_and_late_analysis` | PASS |
| Cairo DST boundaries | `Cairo_calendar_includes_first_actual_instant_across_DST` | PASS, three dates |
| Claim date and department filtering | `New_claim_on_old_item_is_counted_by_claim_date_and_department` | PASS |

Frontend: **47 passed** in eight files; typecheck, lint, production build passed. New behaviors include `requires content review and separate image consent before external analysis`, truthful operational status, and malformed successful-response handling. Worker offline tests: **6 passed** with `python -S -m unittest discover -s worker -v`. Backup safety: **7 passed, 1 skipped** (POSIX permissions on Windows).

Local native Playwright remains BLOCKED: missing expected Chromium binary, then cached executable `spawn UNKNOWN`; two launch failures and ten unrun in each bounded attempt. No application/browser assertion pass is inferred. Prior GitHub PR run verified nine Chromium static cases, then hit three account assertions and stalled Firefox launch; static preview proxy inheritance and cross-engine executable selection are corrected, pending rerun.

The actual-model worker job, all-row restoration verifier, synthetic storage-policy proof, media crypt roundtrip and VM-container checks are configured but **not yet observed passing**. Actual hosted deployment/mail/provider/private storage and complete recovery remain BLOCKED. Earlier 67-case evidence below is historical.

## Earlier mail/hosting batch

| Requirement | Evidence | Result |
|---|---|---|
| Existing backend behavior and new hosting/mail changes | `deployment-baseline.trx`: 67 passed, 0 failed, 0 skipped against local PostgreSQL 16/pgvector | PASS for the exercised cases |
| HTTPS email contract and retry identity | `Resend_sends_only_expected_fields_and_preserves_retry_identity` | PASS, isolated HTTP boundary |
| Missing email setup blocks external calls | `Unconfigured_email_never_contacts_provider` | PASS |
| Provider errors remain distinct and sanitized | `Email_provider_failures_are_classified_without_leaking_response` | PASS |
| Invalid/oversized provider success bodies are rejected | `Invalid_email_success_response_is_rejected`, `Oversized_email_response_is_rejected` | PASS |
| Unsafe hosted configuration fails validation | `Unsafe_hosted_configuration_is_rejected_without_exposing_values` | PASS |
| Explicit secure hosted configuration is accepted | `Hosted_configuration_accepts_explicit_https_persistent_storage_and_verified_database_tls` | PASS for validation only |
| Persisted encrypted keys survive process replacement | `Certificate_encrypted_keys_decrypt_existing_payload_after_provider_restart` | PASS, independent local providers |

The targeted hosting/mail run passed 31 cases. A subsequent solution build completed with zero warnings/errors, and frontend type checking passed. After SMTP timeout/development sender handling was tightened, the full backend suite again passed 67 cases with zero failures/skips (`deployment-mail-keys-final.trx`); frontend lint also passed.

Five backup safety tests passed: `test_backup_refuses_repository_destination_before_running_tools`, `test_backup_requires_explicit_source_before_creating_directory`, `test_restore_rejects_remote_or_non_test_targets_before_opening_archive`, `test_restore_rejects_tampered_or_traversing_archive_before_database_access`, and `test_restore_does_not_modify_nonempty_target`. These use synthetic integrity fixtures and process boundaries; they do not prove real encryption or database restoration.

The Linux CI workflow is configured for PostgreSQL 17, Chromium/Firefox/WebKit, frontend checks, worker boundary tests, dependency/history scans, real encrypted database backup/isolated restore and an API Docker build. Discovery found 36 static and six integrated browser cases across the three engines. A configured workflow or discovered test is not an executed result. GitHub execution, real email, hosted storage/database, real model inference, performance and restore verification remain pending.

TRX evidence is generated under the parent task's non-versioned `work/test-results`; selected sanitized reports are copied into `docs/evidence` when delivering a verified milestone.
