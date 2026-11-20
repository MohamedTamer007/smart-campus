# Release status — 20 November 2026

## Source and deployment identity

- Repository: [MohamedTamer007/smart-campus](https://github.com/MohamedTamer007/smart-campus)
- Source baseline: main, commit b427e37b958d46d9f9348d63791a5e742cf8068f (18 November 2026).
- PR #19 merged the editable photo-description draft flow for found-item registration, maintenance reporting and lost-item search.
- [CI run 44342975370](https://github.com/MohamedTamer007/smart-campus/actions/runs/44342975370) on PR #19's head commit completed successfully.
- [Deployment run 44348910144](https://github.com/MohamedTamer007/smart-campus/actions/runs/44348910144) was manually dispatched against the same main commit and completed successfully on 18 November 2026.

The deployment log records: pre-deployment HTTPS liveness passed; WebDeploy dry-run passed; compiled application uploaded; post-deployment HTTPS /health/live passed. This is strong evidence that the named source was built and uploaded. It does not prove all authenticated workflows, email, AI, private media, storage policies, worker continuity or human acceptance.

This release branch changes the MonsterASP push trigger to main, so a merge to main can deploy automatically. The existing MONSTERASP_AUTO_DEPLOY_DISABLED=true variable remains the emergency disable switch. The deployment script preserves hosted configuration/data, excludes local/production secret files, runs a dry-run and refuses to upload if the current public API liveness check fails.

## Latest CI results

[PR #19 CI run 44342975370](https://github.com/MohamedTamer007/smart-campus/actions/runs/44342975370):

| Check | Recorded result |
|---|---:|
| Backend tests on isolated PostgreSQL 17/pgvector | 280 passed, 4 skipped, 0 failed |
| Frontend tests | 130 passed |
| Existing browser regression checks | 51 passed |
| Integrated browser journeys | 9 passed |
| Database backup and isolated restore | Passed |
| Media encryption/restore round-trip | Passed |
| Isolated storage-policy fixture checks | Passed |
| Backup safety tests | Passed |
| NuGet vulnerability audit | Passed with no vulnerable package reported |
| Production npm audit (npm audit --omit=dev) | 0 vulnerabilities |

The four skipped backend cases are opt-in external-provider or saved-vector evaluation tests; they are not counted as passing live-provider checks. The CI log also reports **11 high-severity npm development-dependency advisories** during installation. The workflow audits production dependencies separately and currently reports zero production vulnerabilities. The recorded picomatch advisory had no patched version in that run; re-check the dependency tree when an upstream fix is available. Do not force-upgrade the toolchain without running the full suite.

## Hosting, privacy and data restrictions

The public preview uses MonsterASP Free. The current [MonsterASP Terms of Service, clause 3.2](https://www.monsterasp.net/Terms/) say Free plans are for development, testing, learning and evaluation only; they are not intended for production workloads or production processing of personal data, have no service/availability commitment, and are not backed up by MonsterASP. Clause 8.2 states that the provider's data-processing agreement applies to paid services.

**The Free website must contain synthetic/fictional test data only. Do not collect real student names, university email addresses, credentials, custody records or photographs there.** A paid plan or university-managed host, appropriate data-processing terms, university privacy/legal approval, a lawful basis, verified storage authorization, backups and restore testing are required before real student data is used. A paid plan alone does not supply the university's legal basis or approvals.

No host environment values, database secrets, storage keys, API keys or private deployment credentials were read or changed during this review. The successful deployment run does not certify the live environment configuration or hosted storage policies.

## AI and retrieval status

AI integrations are present in source, but checked-in safe defaults keep external processing disabled:

- AI:AllowNonSensitiveInference=false
- AI:AllowNonSensitiveImages=false
- AI:AllowItemPhotoDescription=false
- Search:ExternalProcessingApproved=false
- Search:ExternalImagesApproved=false
- Gemini external-processing/image approvals are false.
- Search:CalibrationApproval is empty.

The code supports OpenRouter/Gemini paths, a non-AI maintenance triage fallback, keyword search fallback and editable AI photo-description drafts. This does not mean those external services are enabled on the host. Do not switch the flags on for real personal data on the Free plan.

Lost-and-found suggestions remain distinct from calibrated retrieval. The small Gemini experiment used 20 fictional requests; six positive queries ranked correctly Top-1/Top-5, but both image positives reused the same gallery pixels. Two no-match queries were **Not Evaluated** because no independently calibrated rejection threshold was approved. No reviewed campus dataset or calibrated no-match threshold is approved. Similarity percentages are numerical ranking signals, not probabilities of identity or ownership. See [SEARCH_CANDIDATES_MODE.md](SEARCH_CANDIDATES_MODE.md) and [the Gemini experiment](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md).

## Open observations and limitations

- **O-01:** photo-only search was accepted against the prior hosted build; the required-description rule was corrected and deployed on 14 November 2026.
- **O-02:** the maintenance transition failure is explained by the server's assignee_required rule; the UI's former generic validation message obscured the cause.
- **O-03:** the original found-item registration attempt with a synthetic PNG returned a generic validation response. A later attempt with different fictional inputs succeeded, but that does not identify the original cause. This release improves browser-side image decoding/dimension checks and displays safe, actionable messages for known server error codes. The historical O-03 cause remains **unconfirmed** until the original scenario is reproduced with a known-good image and captured server code.
- **O-04:** the account-deletion option was removed from the interface on 14 November 2026; the backend endpoint remains by the project owner's recorded decision.

Still open: verified campus directory/staff scopes, hosted private-storage policy verification, real email delivery/expiry journeys in the approved environment, human usability and screen-reader acceptance, larger-scale load testing, independent retrieval calibration/no-match rejection, and the full operational recovery/backup plan for the selected production host.

## Release conclusion

The evidence supports a **controlled synthetic-data academic evaluation demo**: the current source passed CI, the named main commit was uploaded successfully, and public API liveness passed after deployment. It does **not** support a claim of 10/10 production readiness, live AI quality, or authorization to process real student personal data on MonsterASP Free.
