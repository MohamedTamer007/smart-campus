# Owner-authorized similarity suggestions

On 14 November 2026 the project owner requested that hosted search operate before retrieval calibration is approved. The deployed non-secret defaults therefore set `Search:AllowUncalibratedCandidates` to `true`.

If the current query mode has no approved threshold, hosted search returns at most five eligible items ranked by the existing Gemini similarity calculation, without applying a score cutoff. Unrelated items can appear. The API returns `calibrated: false` and `calibration: null`; the Arabic and English search page labels these results as uncalibrated suggestions. This mode does not meet the previously specified calibrated No-Match acceptance criterion and does not establish retrieval accuracy or ownership.

Provider configuration, separate text/image transfer approvals, free-tier assertion, quotas, current model/revision/dimension checks, indexing readiness, item visibility filters, expiring result grants and custodian claim review remain enforced. No API key is committed and no new provider or paid fallback is introduced.

When a valid threshold exists for the current mode/revision, it is still applied and the API returns `calibrated: true`. The administrator's calibration indicator continues to report actual threshold approval, independently of whether suggestion mode is enabled.

Set the hosting variable `Search__AllowUncalibratedCandidates=false` to restore mandatory calibration. Missing approval then returns `search_calibration_required` before any query embedding is requested. This setting does not resolve `search_provider_not_configured`, `search_index_pending` or provider/quota errors.
