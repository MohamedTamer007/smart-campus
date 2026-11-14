# Lost & Found search candidate mode

## Default behavior — 15 November 2026

Hosted Lost & Found search is fail-closed when the active query mode has no independently reviewed threshold for the current Gemini embedding revision. The checked-in non-secret default is:

`Search:AllowUncalibratedCandidates=false`

Without an approved current-revision threshold, the API returns `search_calibration_required` before requesting query embeddings. The page explains that matching quality has not yet been approved instead of presenting the top five candidates as if they were useful matches.

This change does not invent or hard-code a cosine-similarity threshold. Gemini embedding similarities are ranking signals, not calibrated probabilities; text-only, image-only, and combined queries need separate evaluation before thresholds can be approved.

## Explicit uncalibrated testing

The previous owner-authorized suggestion mode can still be enabled explicitly with `Search:AllowUncalibratedCandidates=true`, but it returns up to five candidates without a score cutoff and may include unrelated items. Use that only with synthetic test data in a controlled evaluation, not as a production-quality match signal. Responses remain marked `calibrated: false`.

A valid reviewed threshold for the active query mode and exact model revision always takes precedence. Calibration approval for an older model/revision does not authorize the current Gemini vector space.

## Search result eligibility

User-facing search excludes:
- Items uploaded by accounts carrying the `smartcampus:demo-only` marker.
- Explicit documentation fixtures whose description begins with `DEMO ONLY` or `DOCUMENTATION TEST ONLY`.

These records are not deleted or changed; they are simply ineligible for Lost & Found search and do not block search while their embeddings are pending.

## Hosting precedence

The application reads `smartcampus.defaults.json` before environment variables. Hosting environment variables have higher priority, so an existing `Search__AllowUncalibratedCandidates=true` override would supersede the checked-in default. Keep that override absent or set it to `false` to retain the fail-closed behavior.

Provider configuration, privacy approvals, free-tier assertion, quotas, model/revision/dimension checks, indexing readiness, item visibility, private media access, temporary result grants, and custodian claim review remain enforced. No API key, production record, or user data is included in this change.
