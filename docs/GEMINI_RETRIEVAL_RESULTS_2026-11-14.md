# Gemini retrieval results — 14 November 2026

Real direct Google Gemini API requests using `gemini-embedding-2`, revision `gemini-embedding-2-google-768-v1`, 768 dimensions. Fictional descriptions and generated illustrations only. This is a small integration/ranking experiment, not independent production calibration.

- Actual provider calls: **20** (12 gallery embeddings and 8 query embeddings).
- Completed: **20**; provider errors/timeouts: **0** in this run. One application minute-budget pause was respected.
- Positive queries: **6**; Top-1 **6/6**; Top-5 **6/6**. Four text queries and two identical-image lookups.
- The image positives reuse gallery pixels, so their score near 1 does not establish matching across different photographs.
- No-match queries: **2**; false acceptance is **Not Evaluated** because no independently calibrated threshold is approved.
- Mean embedding step latency: **617.5 ms**, range **463–949 ms**; excludes quota waiting and host setup.

## Queries

| Case | Expected | First result | Raw cosine | Top-1 | Top-5 | ms |
|---|---|---|---:|---|---|---:|
| query-01-text | bottle | bottle | 0.871612 | True | True | 480 |
| query-02-text | headphones | headphones | 0.805438 | True | True | 505 |
| query-03-text | umbrella | umbrella | 0.831878 | True | True | 477 |
| query-04-text | wallet | wallet | 0.860503 | True | True | 501 |
| query-05-image | bottle | bottle | 1.000000 | True | True | 687 |
| query-06-image | watch | watch | 1.000001 | True | True | 739 |
| query-07-absent-text | No match | wallet | 0.690440 | Not Evaluated | Not Evaluated | 496 |
| query-08-absent-image | No match | watch | 0.757261 | Not Evaluated | Not Evaluated | 768 |

## All ranked results

### query-01-text

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | bottle | 0.871612 |
| 2 | watch | 0.676261 |
| 3 | notebook | 0.635311 |
| 4 | umbrella | 0.630743 |
| 5 | headphones | 0.582514 |
| 6 | wallet | 0.572869 |

### query-02-text

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | headphones | 0.805438 |
| 2 | bottle | 0.614123 |
| 3 | watch | 0.591152 |
| 4 | wallet | 0.578344 |
| 5 | umbrella | 0.577446 |
| 6 | notebook | 0.566301 |

### query-03-text

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | umbrella | 0.831878 |
| 2 | bottle | 0.586483 |
| 3 | wallet | 0.575811 |
| 4 | notebook | 0.564925 |
| 5 | watch | 0.533243 |
| 6 | headphones | 0.523766 |

### query-04-text

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | wallet | 0.860503 |
| 2 | notebook | 0.572205 |
| 3 | umbrella | 0.560508 |
| 4 | watch | 0.543362 |
| 5 | bottle | 0.537740 |
| 6 | headphones | 0.489472 |

### query-05-image

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | bottle | 1.000000 |
| 2 | watch | 0.837831 |
| 3 | headphones | 0.818941 |
| 4 | notebook | 0.806169 |
| 5 | wallet | 0.802771 |
| 6 | umbrella | 0.797529 |

### query-06-image

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | watch | 1.000001 |
| 2 | bottle | 0.837831 |
| 3 | headphones | 0.830234 |
| 4 | umbrella | 0.828177 |
| 5 | notebook | 0.820383 |
| 6 | wallet | 0.804250 |

### query-07-absent-text

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | wallet | 0.690440 |
| 2 | umbrella | 0.594973 |
| 3 | notebook | 0.575797 |
| 4 | bottle | 0.572986 |
| 5 | watch | 0.537581 |
| 6 | headphones | 0.500314 |

### query-08-absent-image

| Rank | Candidate | Raw cosine |
|---:|---|---:|
| 1 | watch | 0.757261 |
| 2 | wallet | 0.752063 |
| 3 | umbrella | 0.728254 |
| 4 | bottle | 0.718269 |
| 5 | notebook | 0.705145 |
| 6 | headphones | 0.697923 |

## Evidence and release boundary

- Live test: `GeminiRetrievalLiveTests.Saved_lost_found_gallery_runs_through_the_real_application_with_at_most_20_requests` — passed 1/1.
- TRX: `backend/SmartCampus.Tests/TestResults/gemini-retrieval-live-20261114.trx` (local ignored test artifact).
- Saved-vector replay: `CachedLostFoundEvaluationTests.Saved_embedding_gallery_replays_through_application_ranking_without_external_calls` — passed 1/1, unchanged provider usage.
- The test-only data is persisted in the isolated local `_test` database; a local schema backup was saved outside Git before further database tests.
- Combined text/image retrieval and non-identical image matches still require independent real-vector quality acceptance. Production search approvals and thresholds remain unchanged.
- The test key was held only in process memory and cleared when the runner exited; no DPAPI key file was created.
