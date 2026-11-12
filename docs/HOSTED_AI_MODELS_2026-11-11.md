# Smart Campus selected hosted models — updated 13 November 2026

**Draft / not production-approved.** This branch has not been deployed to SnapDeploy or MonsterASP.NET. Do not copy API keys into Git or chats.

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
AI__SyntheticSmokeTestsEnabled=false
AI__SyntheticUserSmokeTestsEnabled=false
# Provide both secrets only in the host's private environment settings:
# AI__OpenRouterKey=<secret>
# AI__NvidiaKey=<secret>
AI__OpenRouterDailyRequestLimit=50
AI__OpenRouterRequestsPerMinute=20
AI__NvidiaDailyRequestLimit=0
AI__NvidiaRequestsPerMinute=5
Search__Provider=hosted
Search__ExternalProcessingApproved=false
Search__ExternalImagesApproved=false
Search__CalibrationApproval=
# Search__MinimumScore must only be set after real-world calibration
```

The application defaults now provide maintenance labels `plumbing`, `water`, `electrical`, `hvac`, `lighting`, `furniture`, `doors`, `safety`, `other`, with `low`/`medium`/`high` priority and risk values. These are broad suggestions drawn from the project examples, not an institutional taxonomy; environment values may override them when the university supplies exact labels. Staff still approve every report and the specific image before transfer, and AI output remains separate from the saved staff decision. On the hosted environment `AI__Provider=openrouter` routes both text and approved images to the exact `:free` Super VL model. OpenRouter chat requests pin that model, set `allow_fallbacks=false` and set maximum prompt/completion prices to zero. Embeddings remain pinned to `nvidia/nemotron-3.5-embed-8b:free` with the same zero-price/no-fallback controls. The unused NVIDIA settings are left intact and are not called when provider selection is `openrouter`.

Before enabling hosted retrieval:
1. Approve **external transfer of even description text**, including the actual university data classification, provider policies, and sender/recipient obligations. Only then set `Search:ExternalProcessingApproved=true`. The code does not redact personally identifying data.
2. **Do not send student photos to the free embeddings endpoint until data governance confirms this is allowed.** OpenRouter's free Nemotron endpoint explicitly warns against personal/confidential information and may log model inputs. `Search:ExternalImagesApproved` must remain false if that condition is not satisfied.
3. Confirm the provider accepts exact OpenRouter request bodies for both text and images with *real API calls using synthetic, non-sensitive test inputs*. The admin OpenRouter diagnostic sends four bounded requests (text classification, image classification, text embeddings, image embeddings), validates taxonomy values, and uses only a fixed fictional report and generated image. The application daily/minute budget applies to each provider request; this does not bypass OpenRouter's own free-plan limits.
4. Run fresh full indexing of available found-items using the new model/revision. All new vectors use revision `nemotron35-8b-openrouter-1024-v1` (1024 truncated normalized dimensions); older SigLIP2 vectors are isolated by revision. Do not reuse an old search score threshold.
5. Validate mixed Arabic and English examples, text-only queries, image search, no-match false positives, common-object collisions, claimant privacy, and end-to-end latency. Record a reviewer-approved score threshold and only then set `Search:CalibrationApproval`, `Search:MinimumScore`, and `Search:CalibrationRevision=nemotron35-8b-openrouter-1024-v1`. Keep the existing search fail-closed before approval.
6. Validate daily/minute budgets, private media authorization, database vector storage, job retry behavior, failure codes and real service availability before opening access.

The Nemotron embedding model is trained/evaluated mostly for document-image retrieval. Its performance on photos of ordinary lost objects **is unknown**. Do not claim it is interchangeable with SigLIP2 without that evaluation. In particular, NVIDIA's text queries use `query` and indexed descriptions/images use `passage`; image queries use `passage` and require separate calibration.

The `provider.data_collection=deny` routing parameter is included in the outgoing request to avoid opt-in data collection, but **does not override a free provider's own published logging policy**. Providers that cannot satisfy the restriction may reject the request. Do not bypass the safety gate to make a free model accept private photos.

## Synthetic-only maintenance AI smoke-test plan

This is a **procedure, not a passed live test**. Neither `/health/live` nor
`/health/ready` makes an AI-provider request. Never run this against real
student reports or the existing production database on MonsterASP.NET Free.

1. Use a separate, non-personal-data evaluation database and storage project,
   approved synthetic staff/test accounts, and the non-production feature build.
   Confirm that the two server-only API keys exist in the host's private
   environment settings; never copy their values into screenshots or GitHub.
2. Have the owner/staff reviewer approve a maintenance classification taxonomy.
   The following is an **illustrative fictional test taxonomy only**, not an
   authorized university classification:

   ```ini
   # Synthetic-only example; requires explicit operator permission before activation
   AI__Provider=openrouter
   AI__AllowNonSensitiveInference=true
   AI__AllowNonSensitiveImages=false
   AI__FaultTypes__0=plumbing
   AI__FaultTypes__1=electrical
   AI__FaultTypes__2=hvac
   AI__Priorities__0=low
   AI__Priorities__1=medium
   AI__Priorities__2=high
   AI__Risks__0=low
   AI__Risks__1=medium
   AI__Risks__2=high
   ```

   The synthetic OpenRouter diagnostic needs a positive OpenRouter daily and
   per-minute budget of at least four requests. NVIDIA's key and budgets are
   not used by this provider selection.
3. The admin-only `openrouter-smoke` route uses fixed fictional text and a
   generated image to validate text classification, image classification,
   text embeddings and image embeddings. It requires
   `AI__AdminSyntheticDiagnosticEnabled=true`, and is protected by admin auth
   and CSRF. Four requests are charged to the app's regular OpenRouter daily
   and minute budgets. It returns only validated taxonomy labels and model
   IDs, never raw model replies, report content or credentials. It does not
   require enabling external inference for actual reports or enabling public
   retrieval.
4. **Optional regular-account acceptance check (no Admin role needed):**
   on a separate *controlled* test deployment only, add the approved university
   test mailbox to `Release:AllowedEmails`, and set both
   `AI__SyntheticSmokeTestsEnabled=true` and
   `AI__SyntheticUserSmokeTestsEnabled=true`. A verified allowed test account
   can open `/maintenance` and click **Test maintenance AI**. The API
   `GET /api/v1/ai/smoke` advertises the feature without showing secrets.
   `POST /api/v1/ai/smoke` sends **only a hard-coded fictional text**, no report,
   photo, account details, or user-provided message. It is authenticated,
   CSRF-protected, and limited by atomic database counters to one attempt per
   account per UTC day and three attempts total per UTC day; the existing
   provider budget is charged as well. A validated response only proves the
   configured text model worked with that example. Turn both smoke switches
   off again after the test. This feature is not a substitute for staff review
   of real reports or loss/found retrieval calibration.
5. Have an authorized synthetic student submit a **text-only fictional**
   maintenance report and an authorized scoped staff member review it, confirm
   it is non-sensitive, and select text-only AI review. The asynchronous job
   should route to OpenRouter `nvidia/nemotron-3.5-super-vl:free`, and a completed suggestion
   must remain distinct from staff's actual operational decision.
6. Observe the job/assessment state through the authorized API and worker
   operations. A quota, provider failure or malformed response is a failed
   acceptance test; do not call a pending/unavailable result a success.
7. **Keep image inference disabled** until a separate review authorizes the
   exact synthetic image and provider transfer. In the source code,
   `AI:Provider=auto` routes staff-approved images to OpenRouter, and the
   `AI:AllowNonSensitiveImages` gate prevents images being forwarded otherwise.
   Approved JPEG/PNG/WebP bytes are validated and converted to a JPEG data
   URL by the API; the original uploaded metadata is not sent to the model.
8. Hosted lost/found embeddings require their *own* provider compatibility
   tests, externally approved data processing and a separately measured
   `Search:MinimumScore`/calibration approval. Text analysis success does not
   enable or validate retrieval.

`AI__OpenRouterKey` and `AI__NvidiaKey` are host environment variable names,
not values to commit to any tracked file. The code change is not automatically
published by a branch push.

## Sources
- https://openrouter.ai/nvidia/nemotron-3.5-embed-8b:free
- https://openrouter.ai/docs/api/api-reference/embeddings/submit-an-embedding-request
- https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3_5-embed-8b-infer
- https://openrouter.ai/nvidia/nemotron-3.5-super-vl:free
