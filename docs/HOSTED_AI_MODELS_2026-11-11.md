# Smart Campus selected hosted models — 11 November 2026

**Draft / not production-approved.** This branch has not been deployed to SnapDeploy or MonsterASP.NET. Do not copy API keys into Git or chats.

## Fixed model routing

| Use | Provider | Exact model ID | API endpoint |
|---|---|---|---|
| Maintenance, reviewed text only | NVIDIA | \`openai/gpt-oss-20b\` | \`https://integrate.api.nvidia.com/v1/chat/completions\` |
| Maintenance, staff-reviewed non-sensitive images | OpenRouter | \`nvidia/nemotron-3.5-super-vl:free\` | \`https://openrouter.ai/api/v1/chat/completions\` |
| Found/lost image + description retrieval (unapproved) | OpenRouter | \`nvidia/nemotron-3.5-embed-8b:free\` | \`https://openrouter.ai/api/v1/embeddings\` |

All calls are made from ASP.NET Core. **No local Python image-inference worker is needed when \`Search:Provider=hosted\`.** The existing loopback worker code remains for development and historical tests; it is not a hosted dependency. Free model listings and routing availability can change.

## Proposed environment values (do not activate without the safety gates)

\`\`\`ini
AI__Provider=auto
AI__AllowNonSensitiveInference=false
AI__AllowNonSensitiveImages=false
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
\`\`\`

Before enabling maintenance processing, provide a reviewed taxonomy under \`AI:FaultTypes\`, \`AI:Priorities\`, \`AI:Risks\`, staff approve the submitted text and image, select explicit request budgets and authorize the relevant external provider. \`AI:Provider=auto\` **does not switch vendors on failure**: text always uses NVIDIA GPT-OSS and an approved image always uses OpenRouter Super VL.

Before enabling hosted retrieval:
1. Approve **external transfer of even description text**, including the actual university data classification, provider policies, and sender/recipient obligations. Only then set \`Search:ExternalProcessingApproved=true\`. The code does not redact personally identifying data.
2. **Do not send student photos to the free embeddings endpoint until data governance confirms this is allowed.** OpenRouter's free Nemotron endpoint explicitly warns against personal/confidential information and may log model inputs. \`Search:ExternalImagesApproved\` must remain false if that condition is not satisfied.
3. Confirm the provider accepts exact OpenRouter request bodies for both text and images with *real API calls using synthetic, non-sensitive test inputs*. This integration has only mocked provider tests until that happens.
4. Run fresh full indexing of available found-items using the new model/revision. All new vectors use revision \`nemotron35-8b-openrouter-1024-v1\` (1024 truncated normalized dimensions); older SigLIP2 vectors are isolated by revision. Do not reuse an old search score threshold.
5. Validate mixed Arabic and English examples, text-only queries, image search, no-match false positives, common-object collisions, claimant privacy, and end-to-end latency. Record a reviewer-approved score threshold and only then set \`Search:CalibrationApproval\` and \`Search:MinimumScore\`. Keep the existing search fail-closed before approval.
6. Validate daily/minute budgets, private media authorization, database vector storage, job retry behavior, failure codes and real service availability before opening access.

The Nemotron embedding model is trained/evaluated mostly for document-image retrieval. Its performance on photos of ordinary lost objects **is unknown**. Do not claim it is interchangeable with SigLIP2 without that evaluation. In particular, NVIDIA's text queries use \`query\` and indexed descriptions/images use \`passage\`; image queries use \`passage\` and require separate calibration.

The \`provider.data_collection=deny\` routing parameter is included in the outgoing request to avoid opt-in data collection, but **does not override a free provider's own published logging policy**. Providers that cannot satisfy the restriction may reject the request. Do not bypass the safety gate to make a free model accept private photos.

## Sources
- https://openrouter.ai/nvidia/nemotron-3.5-embed-8b:free
- https://openrouter.ai/docs/api/api-reference/embeddings/submit-an-embedding-request
- https://docs.api.nvidia.com/nim/reference/nvidia-nemotron-3_5-embed-8b-infer
- https://openrouter.ai/nvidia/nemotron-3.5-super-vl:free
