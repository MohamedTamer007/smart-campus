# Smart Campus — release evidence matrix (20 November 2026)

This dated matrix explains what is actually evidenced for the academic review. It is **not** certification of production readiness, university approval or permission to process real student information.

| Claim | Evidence and exact source | Status | Limit |
|---|---|---|---|
| PR #21 application release was built and deployed | [Automatic main deployment 44783186261](https://github.com/MohamedTamer007/smart-campus/actions/runs/44783186261), source `eb3c62c2c743ce792a53f311ce97c3751d422437` | Verified | No authenticated end-to-end acceptance on that build |
| Public API health responded after WebDeploy | Same run's post-deployment `/health/live` probe | Verified at probe time | Not continuous uptime or worker health |
| Public build corresponds to PR #21 source | Same run: `Public release commit verified: eb3c62c2c743ce792a53f311ce97c3751d422437` | Verified at probe time | Public commit marker is not a penetration test |
| Fresh independent public read-only probe on 20 Nov 2026 at 20:57:59 UTC | [Public smoke run 44816040794](https://github.com/MohamedTamer007/smart-campus/actions/runs/44816040794); 5/5 offline probe contract tests; live HTTPS `/health/live` and `/build-info.json` | **PASS**; public marker was exactly `eb3c62c2c743ce792a53f311ce97c3751d422437` | No authentication, signup, real email inbox, AI quality, storage policy or production data was evaluated |
| Full automated suite on inspected docs-only main SHA `a48f36e0a70d8b15036a0c1d53e5c57597ed4da7` | [CI 44810975550](https://github.com/MohamedTamer007/smart-campus/actions/runs/44810975550) | Pass: 280 backend, 4 skipped; 140 frontend; 51 browser and 9 integrated | CI and controlled fixtures, not a live-host browser acceptance |
| Isolated database, backups, media and storage-policy fixture checks | [CI 44810975550](https://github.com/MohamedTamer007/smart-campus/actions/runs/44810975550) | Passed on controlled fixtures | Actual hosted private-storage policy and operational restore unverified |
| Current npm vulnerability result at inspected main CI | [CI 44810975550](https://github.com/MohamedTamer007/smart-campus/actions/runs/44810975550) | `npm ci`: 0 vulnerabilities; production audit: 0 | Historical PR #19 run reported 11 high-severity dev advisories; continue dependency review |
| Offline AI evaluation scorer (not provider accuracy) | [Scoring script](../scripts/score_ai_evaluation.py), [ten synthetic tests](../scripts/tests/test_ai_evaluation.py), [CI 44810975550](https://github.com/MohamedTamer007/smart-campus/actions/runs/44810975550) | Ten scorer contracts passed, 22 Python tests overall | No independent real-model evaluation; input cases still missing |
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

## Final academic documentation audit — 20 November 2026

- The native Google Docs thesis contains six chapters, five appendices, 39 images and 34 tables. The 91 bibliographic entries have been reordered alphabetically by verified first author surname, or by title when authorship is unavailable, and presented with hanging indents; their clickable source links were retained. Nine previously incomplete bibliographic entries received verified author/venue/page/DOI details from scholarly records. These are improvements in presentation and metadata coverage, **not** an independent APA 7 certification of all 91 entries.
- An earlier documentation-only main commit `43b687d0122dcdbe99eb3aa4e0b2eef87e6471fa` passed [CI 44794548649](https://github.com/MohamedTamer007/smart-campus/actions/runs/44794548649). The last confirmed hosted application release remains PR #21, `eb3c62c2c743ce792a53f311ce97c3751d422437` ([deployment 44783186261](https://github.com/MohamedTamer007/smart-campus/actions/runs/44783186261)); a documentation change is not a new hosted acceptance test.
- The subsequent documentation-only main commit `a48f36e0a70d8b15036a0c1d53e5c57597ed4da7` passed full [CI 44810975550](https://github.com/MohamedTamer007/smart-campus/actions/runs/44810975550): 280 backend passed (four skipped), 140 frontend passed, 51 browser regressions, nine integrated journeys, and zero npm vulnerabilities on installation and production audit. This is controlled CI, not a new hosted release or live AI/provider acceptance.
- Documentation consistency corrections in the thesis distinguish the verified public source-commit marker from full hosted acceptance, preserve the historical O-03 uncertainty, and describe the bibliographic sort order accurately.
- The eight-tab [literature workbook](https://docs.google.com/spreadsheets/d/1bnA9KCAJtU6XEJCAzpeg6kAx1Loyaj2-TeUN7w5v8BQ/edit) contains 91 team study records. The named raw benchmark package `Chapter5_AI_Deliverables.zip` was not available for independent reproduction in the accessible GitHub, Google Drive or supplied conversation Library sources. Preserve the paper's historical benchmark as team-reported; do not claim it was rerun from its raw case CSV in this audit.
- A 2023–2024 graduation-project template was found among the team's saved Google Drive material and compared at the level of major sections and page setup (US Letter, 1-inch margins). That historical template is **not** an official 2026–2027 acceptance decision; formal institutional requirements and sign-off must still be evidenced.
- Remaining evaluation gates are listed above. They require authentic benchmark data, approved test identities, properly dated hosted run evidence, human/device accessibility and load observations, and authorized institutional decisions. Synthetic narrative, code review or a passing CI run cannot replace those primary records.


## Final thesis reconciliation and current documentation-only CI (20 November 2026)

| Evidence item | Actual observed result | Important boundary |
| --- | --- | --- |
| Native thesis PDF exported after Chapter 2 author–year reconciliation | 148 pages; zero wholly empty pages; 80/80 Table of Contents heading/page matches; 29 numbered Figure 3.1–3.29 captions; 34 native tables; 39 inline images | Pagination and structure are checked; print-scale image readability and human screen-reader accessibility are not certified |
| Author–year and bibliography presence | 91/91 reference entries have a mechanical first-author/year proximity candidate in Chapter 2; two same-author 2008 records distinguished as 2008a/2008b | Not 91/91 publisher/DOI/APA-certified; every attribution and metadata record needs direct source validation |
| Most recent independently inspected documentation-only main CI | [Run 44821312368](https://github.com/MohamedTamer007/smart-campus/actions/runs/44821312368) completed successfully on source commit abe8980104140ca95bb775ed01bb3342b0de2d2d | A docs-only green CI is not a newly deployed application nor hosted user acceptance |
| Public release read-only smoke | [Run 44816040794](https://github.com/MohamedTamer007/smart-campus/actions/runs/44816040794) passed at its probe time for application SHA eb3c62c2c743ce792a53f311ce97c3751d422437 | Liveness and release marker identity only, not production personal data-processing permission, inbox mail, AI quality or live host security |

**Academic status:** Document structure and citation discovery now have reproducible evidence. **Final university acceptance remains unissued**: the 2026–2027 Faculty rubric/template and a dated authorized signature have not been independently established; no unsigned or implied approval should be added to the thesis. The prior institutional-template comparison used a historical 2023–2024 version, not a current-year ruling. The updated 91-entry source-location ledger is [CITATION_CROSSCHECK_2026-11-20.md](CITATION_CROSSCHECK_2026-11-20.md). All open hosted/AI/security/privacy/human-test gates listed above remain open until their primary evidence is captured.
