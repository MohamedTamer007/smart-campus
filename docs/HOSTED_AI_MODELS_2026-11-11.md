# Smart Campus hosted models — Gemini embeddings change 13 November 2026

**Current local implementation; not production-approved.** The branch has previously been deployed to MonsterASP.NET; these local modifications have not. Do not copy API keys into Git or chats.

## Current model selection in the uncommitted branch

| Use | Provider | Model | Endpoint |
|---|---|---|---|
| Maintenance text classification | OpenRouter | `nvidia/nemotron-3.5-super-vl:free` | `https://openrouter.ai/api/v1/chat/completions` |
| Staff-approved maintenance image analysis | OpenRouter | `nvidia/nemotron-3.5-super-vl:free` | `https://openrouter.ai/api/v1/chat/completions` |
| Lost/found text and image embeddings | Google Gemini API | `gemini-embedding-2` (768 dimensions) | `https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent` |

Embeddings use `AI__GeminiKey`, `outputDimensionality=768`, and the API's `x-goog-api-key` header. This is a separate provider-specific integration; OpenRouter/Nemotron embeddings are no longer sent by the application. The model revision is `gemini-embedding-2-google-768-v1`, so old Nemotron and SigLIP vectors and calibration cannot match or authorize it. Reindexing is opt-in via `Search__GeminiReindexApproved`; it queues available items missing current-revision description vectors and, when both image approvals are enabled, items with missing current-revision image vectors. Jobs are bounded to three attempts and saved vectors are reused after interruption. A terminal failure is not silently resubmitted: after investigating it, an operator can deliberately increment `Search__GeminiReindexGeneration` (default `0`) to create one new idempotent generation. This does not enable external processing by itself. Text/image processing, data-collection approval, billing status, and calibration are separate gates. All Gemini approval flags default false and existing OpenRouter approval does not imply Google approval.

Google's current pricing table lists Gemini Embedding 2 text and image input as free on the Free Tier, but lists paid-tier prices as well. The application sends requests only to the exact Gemini embedding endpoint and does not retry or fall back; software cannot detect whether the Google project is linked to billing. To ensure zero API cost, use a Google project with billing disabled and keep its request limits within the account's free quota. Limits are configurable as `AI__GeminiDailyRequestLimit` and `AI__GeminiRequestsPerMinute`; API key project quotas and free-tier availability remain external. Google also indicates Free Tier content may be used to improve products; university approval is required before production content is sent. See [official Gemini Embeddings 2 API](https://ai.google.dev/gemini-api/docs/embeddings) and [official Gemini API pricing](https://ai.google.dev/gemini-api/docs/pricing/).

Secret and gates (all gates remain off unless reviewed and explicitly approved):

```ini
# Configure only in the host's private environment settings:
AI__GeminiKey=<secret>
AI__GeminiFreeTierOnly=false
Search__GeminiExternalProcessingApproved=false
Search__GeminiExternalImagesApproved=false
Search__GeminiProviderDataCollection=deny
Search__GeminiReindexApproved=false
Search__CalibrationApproval=
Search__CalibrationRevision=
```

The `GeminiFreeTierOnly` flag is an operator assertion, not a billing-status check. No automated deployment should enable Google transfer until the project billing status and university privacy approvals are verified.

Current query modes are description only, photo only, and description plus photo. Image-only queries send one image embedding request on a cache miss and never invent an empty text query; combined queries need up to two requests. Hosted calibration requires separately reviewed `Search__TextMinimumScore`, `Search__ImageMinimumScore`, and `Search__MultimodalMinimumScore` values for the current revision. Keep all unset until independent acceptance testing passes. Queries with images fail closed while a visible available item has missing current-revision photo vectors.

The owner-provided console output from the local session-only test runner confirmed one successful `GeminiLiveTests` test (zero failed/skipped), covering one fictional Arabic text and one generated image, both with 768-dimensional validated vectors. This establishes integration only; retrieval quality, reviewed thresholds, production approvals, and hosted operation remain unverified. See the dated evidence in [BACKEND_PROGRESS.md](BACKEND_PROGRESS.md).

---

## Archived Nemotron embeddings configuration and results

The material below documents the earlier OpenRouter embeddings experiment only. It must not be used as current configuration, current retrieval accuracy, or calibration evidence.

**Historical routing/diagnostic notes below are not current instructions.** In the 13 November local continuation, maintenance classification was pinned to OpenRouter only, zero price caps and no provider fallback; NVIDIA routing and the synthetic diagnostic controls were removed from the operational application. No production configuration was changed by that local continuation. Use `docs/BACKEND_PROGRESS.md` and the current source as the latest implementation record. Public lost-item search remains blocked until its privacy and calibration gates are actually approved.

## Fixed model routing

| Use | Provider | Exact model ID | API endpoint |
|---|---|---|---|
| Maintenance, staff-reviewed non-sensitive text | OpenRouter | `nvidia/nemotron-3.5-super-vl:free` | `https://openrouter.ai/api/v1/chat/completions` |
| Maintenance, staff-reviewed non-sensitive images | OpenRouter | `nvidia/nemotron-3.5-super-vl:free` | `https://openrouter.ai/api/v1/chat/completions` |
| Found/lost image + description retrieval (unapproved) | OpenRouter | `nvidia/nemotron-3.5-embed-8b:free` | `https://openrouter.ai/api/v1/embeddings` |

All calls are made from ASP.NET Core. **No local Python image-inference worker is needed when `Search:Provider=hosted`.** The existing loopback worker code remains for development and historical tests; it is not a hosted dependency. Free model listings and routing availability can change.

## Proposed environment values (do not activate without the safety gates)

```ini
AI__Provider=openrouter
AI__AllowNonSensitiveInference=false
AI__AllowNonSensitiveImages=false
# Keep the key only in the host's private environment settings:
# AI__OpenRouterKey=<secret>
AI__ProviderDataCollection=deny
AI__OpenRouterDailyRequestLimit=50
AI__OpenRouterRequestsPerMinute=20
Search__Provider=hosted
Search__ProviderDataCollection=deny
Search__ExternalProcessingApproved=false
Search__ExternalImagesApproved=false
Search__CalibrationApproval=
# Keep both modality thresholds unset until independent, reviewed calibration.
# Search__TextMinimumScore and Search__MultimodalMinimumScore are separate.
```

The application defaults provide taxonomy labels for plumbing, water, electrical, HVAC, lighting, furniture, doors, safety and other, with low/medium/high priority and risk values. These are project-level suggestions, not an approved institutional taxonomy; environment values may override them when the university supplies exact labels. Staff review each report and the selected image before processing, and AI output remains separate from the saved staff decision. OpenRouter chat requests pin the exact `:free` Super VL model, set `allow_fallbacks=false`, set maximum prompt/completion prices to zero, and require `AI:ProviderDataCollection=allow`. Embeddings are pinned to `nvidia/nemotron-3.5-embed-8b:free` with the same price/fallback controls and a separate `Search:ProviderDataCollection=allow` gate. Defaults remain deny. NVIDIA API keys and direct NVIDIA endpoints are not used.

Before enabling hosted retrieval:
1. Approve **external transfer of even description text**, including the actual university data classification, provider policies, and sender/recipient obligations. Only then set `Search:ExternalProcessingApproved=true`. The code does not redact personally identifying data.
2. **Do not send student photos to the free embeddings endpoint until data governance confirms this is allowed.** OpenRouter's free Nemotron endpoint explicitly warns against personal/confidential information and may log model inputs. `Search:ExternalImagesApproved` must remain false if that condition is not satisfied.
3. Confirm the provider accepts exact OpenRouter request bodies for both text and images with *real API calls using synthetic, non-sensitive test inputs*. Use only a separate test key and a bounded local harness; the previous admin/user smoke routes were removed. The application's daily/minute budget applies to every provider request; this does not bypass OpenRouter's own free-plan limits.
4. Run fresh full indexing of available found-items using the new model/revision. All new vectors use revision `nemotron35-8b-openrouter-1024-v1` (1024 truncated normalized dimensions); older SigLIP2 vectors are isolated by revision. Do not reuse an old search score threshold.
5. Validate mixed Arabic and English examples, text-only queries, image search, no-match false positives, common-object collisions, claimant privacy, and end-to-end latency. Record independently reviewed score thresholds for text-only and text-plus-image search; their score distributions differ. Only then set `Search:CalibrationApproval`, both `Search:TextMinimumScore` and `Search:MultimodalMinimumScore`, and `Search:CalibrationRevision=nemotron35-8b-openrouter-1024-v1`. Keep search fail-closed unless both thresholds and the current model revision are approved.
6. Validate daily/minute budgets, private media authorization, database vector storage, job retry behavior, failure codes and real service availability before opening access.

The Nemotron embedding model is trained/evaluated mostly for document-image retrieval. Its performance on photos of ordinary lost objects **is unknown**. Do not claim it is interchangeable with SigLIP2 without that evaluation. In particular, NVIDIA's text queries use `query` and indexed descriptions/images use `passage`; image queries use `passage` and require separate calibration.

OpenRouter's free Super VL endpoint states that it logs usage for security and product improvement and instructs callers not to upload confidential or personal data. The application therefore keeps `AI:ProviderDataCollection` defaulted to `deny`, and maintenance inference fails before budget reservation unless this is explicitly `allow`. This is required in addition to `AI:AllowNonSensitiveInference` and the per-image review gate. The free embedding provider has a separate `Search:ProviderDataCollection` gate, in addition to `Search:ExternalProcessingApproved`; image processing also requires `Search:ExternalImagesApproved`. Keep these settings denied unless university governance explicitly approves the collection/retention terms for the exact input type. See [OpenRouter's Super VL model page](https://openrouter.ai/nvidia/nemotron-3.5-super-vl:free) and [provider routing controls](https://openrouter.ai/blog/insights/model-routing/).

## Current synthetic-test status

The former smoke-test routes and maintenance test button are no longer part of the application. There is no user-facing evaluation/demo page or provider-diagnostic button. `AiEvaluationController` is development-only and is blocked in published environments; it is not an operational route. Operational maintenance AI runs only after user confirmation and staff review of non-sensitive report text, with a separate image-review gate. The admin operations view reports configuration and quota counters without calling an AI provider.

The latest test-key attempt returned HTTP 401 before model inference; it produced no embedding or retrieval ranking. Earlier successful synthetic calls are recorded separately in [ACCEPTANCE.md](ACCEPTANCE.md) and [BACKEND_PROGRESS.md](BACKEND_PROGRESS.md), and do not establish current key validity, retrieval accuracy, or production readiness. Use a valid separate test key only in the local encrypted test-key store, with exact `:free` model IDs, zero price caps, no provider fallback, explicit collection-policy approval, and fictional inputs. Both the app's configured daily/minute budgets and OpenRouter's free-tier limits apply. Never test with real student records or photos.

`AI__OpenRouterKey` is a host environment variable name, not a value to commit to any tracked file. The code change is not automatically published by a branch push.

## Sources
- https://openrouter.ai/nvidia/nemotron-3.5-embed-8b:free
- https://openrouter.ai/docs/api/api-reference/embeddings/submit-an-embedding-request
- https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3_5-embed-8b-infer
- https://openrouter.ai/nvidia/nemotron-3.5-super-vl:free
