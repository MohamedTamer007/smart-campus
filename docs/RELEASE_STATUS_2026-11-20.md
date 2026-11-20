# Release status — 20 November 2026

## Source and deployment identity

- Repository: [MohamedTamer007/smart-campus](https://github.com/MohamedTamer007/smart-campus)
- **Verified application-source release:** PR #21 merged to main as `eb3c62c2c743ce792a53f311ce97c3751d422437` on 20 November 2026. Its automatic deployment [run 44783186261](https://github.com/MohamedTamer007/smart-campus/actions/runs/44783186261) passed on that exact SHA.
- **Hosted verification:** Windows publish, WebDeploy dry-run/upload and post-deployment public `/health/live` checks passed. The workflow log explicitly records `Public release commit verified: eb3c62c2c743ce792a53f311ce97c3751d422437`, based on the public non-secret `build-info.json` marker.
- **Latest independently verified main commit before this documentation-only reconciliation:** `a48f36e0a70d8b15036a0c1d53e5c57597ed4da7`; full [CI run 44810975550](https://github.com/MohamedTamer007/smart-campus/actions/runs/44810975550) passed (280 backend/four skipped; 140 frontend; 51 browser; nine integrated; 22 Python script tests including 10 new AI scorer contracts). A documentation-only commit is not a new application deployment.
- **Historical release evidence:** PR #19 added editable photo-description drafts and was manually deployed on 18 November as `b427e37b958d46d9f9348d63791a5e742cf8068f` ([deployment 44348910144](https://github.com/MohamedTamer007/smart-campus/actions/runs/44348910144)). PR #20 improved image validation; its pre-merge [CI](https://github.com/MohamedTamer007/smart-campus/actions/runs/44767019024) and [Windows package](https://github.com/MohamedTamer007/smart-campus/actions/runs/44767019137) passed.

The deployment workflow preserves hosted configuration/data, excludes secrets and uses the `MONSTERASP_AUTO_DEPLOY_DISABLED=true` emergency stop. Deployment does not apply database migrations. Release identity and liveness do **not** establish hosted privacy/storage policies, authenticated journeys, real mailbox delivery, live AI quality, worker availability, human acceptance or university permission to process personal data.

## CI results and evidence boundaries

Latest application release: PR #21 [CI run 44781998454](https://github.com/MohamedTamer007/smart-campus/actions/runs/44781998454) passed 280 backend tests (four skipped), 140 frontend tests, 51 browser regressions and nine integrated journeys. [Windows package run 44781998433](https://github.com/MohamedTamer007/smart-campus/actions/runs/44781998433) passed. Its merged commit `eb3c62c2c743ce792a53f311ce97c3751d422437` was independently verified on the public host by [automatic deployment 44783186261](https://github.com/MohamedTamer007/smart-campus/actions/runs/44783186261). Subsequent documentation-only main [CI run 44805517934](https://github.com/MohamedTamer007/smart-campus/actions/runs/44805517934) also passed with 280 backend/four skipped, 140 frontend, 51 browser and nine integrated journeys. Neither CI nor release SHA verification establishes full hosted functional acceptance. Historical PR #20 [CI](https://github.com/MohamedTamer007/smart-campus/actions/runs/44767019024) and [package](https://github.com/MohamedTamer007/smart-campus/actions/runs/44767019137) passed before its merge.

Historical [PR #19 CI run 44342975370](https://github.com/MohamedTamer007/smart-campus/actions/runs/44342975370):

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

The four skipped **full CI** backend cases are opt-in external-provider or saved-vector evaluation tests, not live-provider checks. The historical PR #19 npm installation reported 11 high-severity development-dependency advisories despite a clean production audit. The latest inspected main [CI run 44805517934](https://github.com/MohamedTamer007/smart-campus/actions/runs/44805517934) reported **0 vulnerabilities** in npm installation and the production dependency audit, plus a passing NuGet advisory check. Do not transfer an old advisory count into the latest status or force dependency upgrades without full regression testing.

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

The evidence supports a **controlled synthetic-data academic evaluation demo**: the inspected main source passed full CI, PR #21's merged commit was deployed and matched the public release SHA, and public API liveness passed after deployment. It does **not** support a claim of 10/10 production readiness, live AI quality, or authorization to process real student personal data on MonsterASP Free.

## Additional live security receipt — 21 November 2026

On the synthetic-only public preview, [GitHub run 44827616271](https://github.com/MohamedTamer007/smart-campus/actions/runs/44827616271) passed a new anonymous-access denial smoke against the deployed application SHA `eb3c62c2c743ce792a53f311ce97c3751d422437`. Four read-only private endpoints (maintenance, found-items, claims and administrative accounts) each responded **401 Unauthorized** with no redirect; public liveness and build marker passed. The [new check](https://github.com/MohamedTamer007/smart-campus/pull/25) is repeatable on approved future releases. This closes **only guest-read denial for those four routes at that probe time**, not signed-in permission boundaries, private media, inbox delivery, full-host acceptance, AI model quality, privacy, backup or university certification. Do not call it permission for real student data on MonsterASP Free.

## Team-reported university and campus security sponsorship — 21 November 2026

The project team has confirmed that **university project approval has been obtained** and that **campus security is a project sponsor**. This is recorded as a direct team statement; it is not a denial or assumption of missing university support. The written approval, sponsor mandate, permitted data classes, test scope, dates and any separate final examiner decision have not yet been reviewed in the connected sources. The authoritative scope and redacted receipt fields are maintained in [APPROVAL_AND_SPONSORSHIP_EVIDENCE_2026-11-21.md](APPROVAL_AND_SPONSORSHIP_EVIDENCE_2026-11-21.md). Do not conflate academic project approval and sponsorship with independent AI evaluation, production privacy clearance, live third-party AI transfer, final examiners' grades or permission for sensitive active security testing.
