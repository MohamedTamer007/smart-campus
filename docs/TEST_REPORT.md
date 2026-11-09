# Deployment continuation test report

This report separates current observed results from earlier implementation evidence in [ACCEPTANCE.md](ACCEPTANCE.md).

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
