# Smart Campus — reproducible AI and hosted acceptance protocol

**Status: protocol and test specification only, not an execution report.** It preserves the evidence boundary in the thesis and [release status](RELEASE_STATUS_2026-11-20.md). Do not populate result tables or claim metrics until raw cases/outputs are captured from an authorized run.

## 1. Freeze the test contract before any provider calls

Record ISO-8601 UTC run date, exact Git SHA, model/provider identifier, model/vector revision, prompts and schema versions, provider settings and approvals, data source/license, anonymization, seed, frozen labels and file SHA-256 digests. Store test vectors/images privately; save only safely redacted reports. Never upload identity documents, actual student photos, credentials or private descriptions to a free hosted provider.

External AI processing defaults **off** in checked-in configuration. Only a university/privacy-approved environment and independently authorized data may change that state. Do not set `Search:CalibrationApproval` or any provider approval to a fabricated value to obtain better results.

## 2. Maintenance classification, priority and risk — three separate outputs

**Gold standard.** Use consented or fictional report text, with appropriately labeled photos where allowed. Two independent qualified annotators choose fault taxonomy, priority and *separate* risk; measure disagreements and adjudicate a frozen reference. Track Arabic/English, Egyptian colloquial, Arabizi, code-switching, negation, multiple faults, ambiguous/no-fault cases and no-image/present-image strata. Do not reuse automatically generated source priority as physical risk ground truth.

**Schema** (`maintenance_cases.csv`, example header only, no fabricated rows):

```csv
case_id,split,language,image_sha256,description_id,gold_fault_type,gold_priority,gold_risk,annotation_version,annotation_status
```

**Predictions** (`maintenance_predictions.csv`):

```csv
run_id,case_id,model_revision,pred_fault_type,pred_priority,pred_risk,provider_status,latency_ms,error_code
```

A case with provider failure remains a failed/unknown prediction, not an invented successful classification. Report denominators, completion rate, macro/micro F1 and per-class precision/recall for each output; include confusion matrices, abstentions and error examples. Present reliability and staff override statistics separately. Record disagreement examples with source labels, not only aggregate percentages.

## 3. Private lost-item retrieval — open set, no-match and distinct photographs

Freeze query/gallery membership by physical item group so the same item's photographs cannot leak from tuning to final test. Positive photo queries must show a **different photograph** from the gallery image; verify SHA-256 inequality and visually review near duplicates. Also include similar-but-not-identical items, same colors/categories, mislabeled descriptions, Arabic/English mixed queries, items missing entirely and candidates the caller is not authorized to see.

**Gallery** (`gallery.csv`), **queries** (`retrieval_queries.csv`), and ranked outputs (`retrieval_results.csv`) header contracts:

```csv
item_id,group_id,image_sha256,description_id,permission_scope,source_split
query_id,split,expected_item_id_or_absent,text_id,query_image_sha256,language,stratum
run_id,query_id,rank,candidate_item_id,similarity_score,permission_allowed,provider_status
```

Pre-register the ranking configuration and no-match threshold on validation cases. Use **one locked threshold** on the held-out test set. Publish positive-query Recall@1, Recall@5 and MRR (if applicable), negative-query false-acceptance rate, rejection recall, separation of image/text strata, per-group sample sizes, and uncertainty intervals. List timeout/fallback counts and compare calibrated ranking with keyword fallback. A cosine similarity percentage is *not* an identity probability, ownership proof or statistically calibrated confidence.

**Existing limits:** [14 November Gemini trial](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) reports 20 fictional requests and Top-1/Top-5 6/6 in six positive queries, but both positive image queries reused gallery pixels; no-match rejection was **Not Evaluated**. The older [30 October trial](https://docs.google.com/document/d/1D5m4sjIKWRyyNi9lzYtx7YRlCAtxPSvP/edit) tested a different SigLIP-based design, not the currently deployed Gemini model. Neither dataset result can be silently transferred to current release performance.

## 4. Hosted role-separated human acceptance

Use a named authorized test environment/build with synthetic identities and a reviewer. Create a dated pass/fail matrix for each of the following flows, recording observed API status/visible UI state and sanitized trace ID: registration/verification/recovery and expiry, student maintenance reporting, staff assignment and transition, staff-scope denial, found-item upload and custody receipt/decline, competing recovery approvals, both orders of two-sided handover confirmation, private search and inaccessible images, session expiry and CSRF/negative access cases, email delivery and provider-offline fallback. The exact 20 November hosted release has not been independently certified across these flows.

For upload O-03, reproduce the *original* synthetic request with a known-good image and capture backend validation code and storage transaction outcome. Do not mark fixed merely because a changed test case succeeds.

## 5. Human UX, accessibility, load and operations

Accessibility: test keyboard focus/escape paths, form labels/validation, Arabic RTL reading order, screen-reader announcements and color contrast on agreed target browsers/devices. Log real participants, tasks and observations with consent and no identifying data in public artifacts.

Performance: state concurrency, arrival rate, dataset size, environment, request mix, warm-up, duration, latency percentiles (P50/P95/P99), throughput, error rate and bottlenecks. A prior sequential one-client latency number is not a campus-scale load result.

Operations: independently confirm protected hosted storage authorization, mailbox reception, external provider quotas, data-retention terms, off-host encrypted backup and complete isolated restore on the selected approved host. A passing fixture test is not a live operational drill.

## 6. Evidence receipt template

```text
Test ID:
Objective/requirement:
Execution timestamp (UTC):
Git SHA / deployed build / model revision:
Environment and synthetic-data provenance:
Expected output and predeclared acceptance rule:
Actual output and status (PASS / FAIL / NOT RUN / NOT EVALUATED):
Artifacts (redacted logs, case CSV SHA-256, screenshots, CI URL):
Reviewer and independent approval (if required):
Limitation / follow-up:
```

Maintain this record in [ACCEPTANCE.md](ACCEPTANCE.md) or a new dated evidence file. Do not change the historical record in place or claim `100% accuracy` from a small/non-independent sample.

## 7. Execute scores from independently captured CSVs

The standard-library-only scorer is [scripts/score_ai_evaluation.py](../scripts/score_ai_evaluation.py), with **10 synthetic contract tests** in [scripts/tests/test_ai_evaluation.py](../scripts/tests/test_ai_evaluation.py). Both are run by the existing CI's scripts/tests unittest discovery. Perfect results on toy fixtures demonstrate arithmetic and input-validation behavior only, **not** deployed model accuracy.

After freezing adjudicated cases and actual inference outputs, run these commands from the repository root:

    python scripts/score_ai_evaluation.py maintenance --cases maintenance_cases.csv --predictions maintenance_predictions.csv

For retrieval the visible-ranked-results CSV contains only **post-policy displayed** candidates. Add the per-query decision file (header below), so that no match is not confused with a provider error:

    run_id,query_id,decision,provider_status,selected_item_id

Set decision to match, no_match, or error. Every held-out query must have exactly one decision; match must select displayed rank 1; no_match and error expose no candidate rows. An absent target is the literal ABSENT. Score with:

    python scripts/score_ai_evaluation.py retrieval --gallery gallery.csv --queries retrieval_queries.csv --decisions retrieval_decisions.csv --ranks retrieval_results.csv

Outputs include numerator/denominator context, macro-F1 and per-class scores for fault/priority/risk, retrieval Recall@1/@5 and absent false-acceptance/rejection rates, and input-file SHA-256 fingerprints. Unavailable predictions and errors are not counted as success. Cross-split duplicate descriptions, identical query/gallery photo hashes, missing independent annotations and displayed unauthorized candidates are refused. Byte inequality does **not** establish different physical photos: manual near-duplicate review is still required.

**Current status:** scorer implemented in source, live Gemini and OpenRouter quality on a new approved independent case set **NOT RUN**. Never substitute synthetic scorer unit-test output for live inference data or institutional consent.
