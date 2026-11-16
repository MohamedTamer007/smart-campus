# Owner-authorized similarity suggestions

On 14 November 2026 the owner requested hosted search before independent retrieval calibration. `Search:AllowUncalibratedCandidates=true` explicitly enables that mode. The 16 November corrective follow-up below supersedes the provisional independent description/photo floors in commit `75a2ed8`, after the owner reported an existing watch disappearing from results.

## Ranking and recall

Uncalibrated search returns up to five eligible items ordered by the existing Gemini embedding similarity calculation, without the withdrawn 0.75 description / 0.90 photo gates. Those unreviewed gates could reject a genuine item photographed differently. `Search:SuggestionTextMinimumScore` and `Search:SuggestionImageMinimumScore` are no longer read; remove obsolete hosting overrides. Search may again contain unrelated suggestions. This is a recall-oriented suggestion mode, not an approved No-Match decision.

The original weighted ranking remains: description score is `0.7 * text-to-item-image + 0.3 * text-to-finder-description`, falling back to whichever signal exists. With a query photo, the overall score is the mean of that description score and query-photo/item-photo similarity, falling back to the description score if no item image signal exists. Without a query photo, the overall score equals the description score. A separately approved threshold for the active query mode and vector revision still filters this overall score and takes precedence over suggestion mode.

[Google's Gemini embedding guidance](https://ai.google.dev/gemini-api/docs/embeddings) describes a shared embedding space for text and images. This implementation keeps its existing text query/document prefixes and separate image embeddings; no model, prompt, vector revision, provider, billing setting, reindex request, or schema was changed by this follow-up.

## Visible scores

`POST /api/v1/found/search` retains `itemIds`, `rankingVersion`, `calibration` and `calibrated`, and adds ordered `candidates` containing `{itemId, score, textScore, imageScore}` for returned items only. Values are cosine-derived scores bounded to [-1,1] for floating-point drift; non-finite values are errors. A text-only query has null `imageScore`. No raw embeddings or scores of excluded items are returned. New and refreshed grants cover only the returned IDs.

The frontend attaches scores by item ID, preserving the backend order. Each search card shows the overall score plus available description/photo components, formatted as `100 * max(0, score)` percent with at most one decimal. For example, 0.821 is 82.1%; this is a numerical similarity display, **not** an 82.1% probability of the same object or its ownership. Negative similarities display 0%; raw signed values remain in the API. Missing scores from an older backend produce no invented percentage. Malformed or out-of-range score payloads remain a service failure.

Uncalibrated responses still contain `calibrated: false` and `calibration: null`; the interface warns that unrelated items can appear and that the percentage is not a match probability. The owner explicitly requested score display; the earlier prohibition on invented confidence percentages still applies. A high score cannot prove identity, distinguish every brand/color, or authorize recovery.

## Eligibility and test accounts

Search excludes records uploaded by the requester, held personally or in the requester's department custody scope, and items no longer available (reserved or delivered). These rules are unchanged. An uploader searching with their own account will not find their own watch even using identical pixels. The page explains this and links to `/found` (My found items). A real acceptance case must use a second verified, eligible requester account, with an available, fully indexed item held by someone else.

Provider configuration, independent text/image transfer approvals, free-tier assertion, quotas, exact model/revision/dimension checks, indexing readiness, visibility scopes, expiring result grants and custodian review remain enforced. There are no new provider calls beyond the existing query embeddings. Existing grants retain their normal expiration.

## Acceptance and rollback

The [earlier small Gemini experiment](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) used identical pixels for image positives and did not validate different photographs or independent No-Match rejection. The new deterministic regression uses controlled vectors against real PostgreSQL to test ranking, score transport and grants; it does not establish real-model precision or recall. Test Arabic/English descriptions, genuinely different views of the same item, similar watches with different colors/brands, absent items, and the separate requester account before accepting quality.

Set `Search__AllowUncalibratedCandidates=false` to restore mandatory calibration. Missing approval then produces `search_calibration_required` before query embeddings. Valid calibration always retains its approved threshold and current-revision requirement. This setting does not resolve missing provider approvals, pending indexing, account exclusions, or provider/quota errors.
