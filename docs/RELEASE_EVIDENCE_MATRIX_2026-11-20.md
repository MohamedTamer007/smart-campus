# Smart Campus — release evidence matrix (20 November 2026)

This dated matrix explains what is actually evidenced for the academic review. It is **not** certification of production readiness, university approval or permission to process real student information.

| Claim | Evidence and exact source | Status | Limit |
|---|---|---|---|
| PR #21 application release was built and deployed | [Automatic main deployment 44783186261](https://github.com/MohamedTamer007/smart-campus/actions/runs/44783186261), source `eb3c62c2c743ce792a53f311ce97c3751d422437` | Verified | No authenticated end-to-end acceptance on that build |
| Public API health responded after WebDeploy | Same run's post-deployment `/health/live` probe | Verified at probe time | Not continuous uptime or worker health |
| Public build corresponds to PR #21 source | Same run: `Public release commit verified: eb3c62c2c743ce792a53f311ce97c3751d422437` | Verified at probe time | Public commit marker is not a penetration test |
| Full automated suite on last inspected docs-only main SHA `84c3fca9393b1b52d6623a25db0660ccff5209f9` | [CI 44784051657](https://github.com/MohamedTamer007/smart-campus/actions/runs/44784051657) | Pass: 280 backend, 4 skipped; 140 frontend; 51 browser and 9 integrated | CI and controlled fixtures, not a live-host browser acceptance |
| Isolated database, backups, media and storage-policy fixture checks | [CI 44784051657](https://github.com/MohamedTamer007/smart-campus/actions/runs/44784051657) | Passed on controlled fixtures | Actual hosted private-storage policy and operational restore unverified |
| Current npm vulnerability result at inspected main CI | [CI 44784051657](https://github.com/MohamedTamer007/smart-campus/actions/runs/44784051657) | `npm ci`: 0 vulnerabilities; production audit: 0 | Historical PR #19 run reported 11 high-severity dev advisories; continue dependency review |
| Maintenance and lost-and-found functional requirements | [Requirements coverage](REQUIREMENTS_COVERAGE_2026-11-14.md) and [acceptance](ACCEPTANCE.md) | Implemented/tested within specified evidence boundaries | Human and real-role hosted acceptance still incomplete |
| Gemini lost-item ranking | [Gemini experiment](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) | 20 fictional requests completed; 6/6 positive Top-1 and Top-5 | Image positives reused gallery pixels; no-match rejection not evaluated; no university calibration |
| Production personal data | [Release status](RELEASE_STATUS_2026-11-20.md) and [MonsterASP terms](https://www.monsterasp.net/Terms/) | Not approved | MonsterASP Free is synthetic-only; no real student identities, photos or records |

## Open evidence gates, with closure criteria

1. **Hosted role-separated acceptance:** run the agreed student/staff/admin journeys against a SHA-identified approved build; save dated expected/actual results, account-role context and redacted screenshots.
2. **Found-item O-03 root cause:** reproduce the original failed upload using a known-good synthetic image; capture sanitized API validation code, trace, file dimensions/type, record-storage outcome and the retest.
3. **Search and AI quality:** evaluate an independently reviewed, permissioned dataset with truly different photographs of the same objects, distractors and absent queries; record splits, false acceptance, Recall@K, per-class metrics and release-approved thresholds. Do not call similarity a probability.
4. **Privacy and university sign-off:** obtain the institution's directory and staff scopes, approved retention and data-processing terms; independently test the live private-storage policies before real-person data.
5. **Email and services:** verify inbox receipt, verification, recovery, expiry/replay, provider fallbacks and failure handling in an approved environment; never publish credentials or personal test records.
6. **Human accessibility and load:** record keyboard/screen-reader/RTL/mobile tests, participant tasks, workload definition, percentiles and failure rates.
7. **Operational readiness:** prove off-host encrypted backup and restored service from a drill; document monitoring, incident response, rollback owner and release change log.

## Release interpretation

Three distinct outcomes must be recorded for every requirement: **implemented in source**, **passed in a named controlled/CI test**, and **verified on the named hosted release**. One state never implies the other. The source and deployment status above are current *as reviewed on 20 November 2026*; future runs require new evidence rather than silently reusing these counts.
