# Owner-authorized similarity suggestions

On 14 November 2026 the project owner requested that hosted search operate before retrieval calibration is approved. The deployed non-secret defaults therefore set `Search:AllowUncalibratedCandidates` to `true`.

The 16 November 2026 source follow-up adds provisional filters to this mode; it is not deployed by editing this document. If the current query mode has no approved threshold, hosted search returns at most five eligible items that pass the filters below, ranked by the existing Gemini similarity calculation. It may return fewer than five, including none. The API still returns `calibrated: false` and `calibration: null`; the Arabic and English search page labels these results as uncalibrated suggestions. This mode does not meet the previously specified calibrated No-Match acceptance criterion and does not establish retrieval accuracy or ownership.

| Setting | Default | Evidence required from each candidate |
|---|---:|---|
| `Search:SuggestionTextMinimumScore` | 0.75 | Query description versus the finder's description |
| `Search:SuggestionImageMinimumScore` | 0.90 | Query photo versus a stored item photo, only when the query includes a photo |

Both gates must pass for a description-plus-photo search. High image similarity cannot compensate for an unrelated description, and strong description similarity cannot compensate for a weak photo. Filtering happens in PostgreSQL before the five-result limit and before creating or refreshing result grants. Existing grants keep their normal expiration; this change does not retroactively revoke them. A missing required vector cannot pass its gate.

These are exploratory filters for the pinned `gemini-embedding-2-google-768-v1` vectors, not approved calibration. The [recorded six-positive/two-absent experiment](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) reported raw text-query top scores of 0.805438–0.871612 versus 0.690440 for its absent-text query. Image lookups reused identical pixels and scored near 1, while unrelated images reached 0.837831. The defaults lie between those observations; that experiment does **not** validate these description-to-description gates, the production weighted score, combined queries, different photographs, or color/brand discrimination. Real matches may be excluded and unrelated matches can still pass. Do not transfer these values to another model/revision as calibration evidence. Independent representative acceptance remains required.

Hosting overrides use `Search__SuggestionTextMinimumScore` and `Search__SuggestionImageMinimumScore`. Values must be finite numbers greater than zero and at most one. Invalid values return HTTP 503 `search_filter_not_configured` before query embeddings are requested; the UI reports a configuration failure, not an empty search. Missing values use the defaults. No threshold is exposed as a confidence percentage.

Provider configuration, separate text/image transfer approvals, free-tier assertion, quotas, current model/revision/dimension checks, indexing readiness, item visibility filters, expiring result grants and custodian claim review remain enforced. No API key is committed and no new provider or paid fallback is introduced.

When a valid threshold exists for the current mode/revision, it is still applied and the API returns `calibrated: true`. The administrator's calibration indicator continues to report actual threshold approval, independently of whether suggestion mode is enabled.

Set the hosting variable `Search__AllowUncalibratedCandidates=false` to restore mandatory calibration. Missing approval then returns `search_calibration_required` before any query embedding is requested. This setting does not resolve `search_provider_not_configured`, `search_index_pending` or provider/quota errors.
