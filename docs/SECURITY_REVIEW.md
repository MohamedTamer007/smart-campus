# Security and quality review

Security-sensitive: YES. Reviewer: Codex primary agent. All ten OWASP categories were examined against changed authentication, API, persistence, upload, provider, worker and frontend-boundary code. This is a source review plus the tests in ACCEPTANCE.md, not a penetration test.

| Category | Review result |
|---|---|
| A01 access control | Current verified identity, roles and department scopes on service operations; resource ownership/private search grants/media authorization; no client actor authority. Negative and cross-department tests passed. |
| A02 cryptography | Identity password hashing, secure production cookies, encrypted tickets/outbox, hashed handles, persisted keys and Windows DPAPI. Production TLS/key-volume configuration remains an operator gate. |
| A03 injection | Parameterized EF/Npgsql requests; vector catalog schema quoted in migration; React escapes displayed text; no user-controlled shell commands or HTML rendering. |
| A04 design | Shared item lock and database uniqueness protect approval/transfer/closure; both confirmation orders and stale writes tested. |
| A05 configuration | Loopback development/worker, same-origin API, CSRF, controlled proxy trust, no automatic migrations; local secrets and demo receipts excluded from publish/archive. |
| A06 dependencies | npm/NuGet audits clear. Worker direct pins updated to advisory-free metadata versions; actual full worker environment and transitive validation remain blocked. |
| A07 authentication | Verified email, lockout/rate limits, token validation, session expiry/revocation and reset replay tests. Expired-token validation passed using token-policy overrides. |
| A08 integrity | Pinned model revision, safetensors-only loading, no remote model code, strict structured suggestions and vector metadata validation. Real pretrained execution unverified. |
| A09 monitoring | Structured login/domain/grant audits; stable rejection/failure codes and trace IDs without passwords, tokens, images or provider payloads. Operational alerting is not deployed. |
| A10 SSRF | Worker accepts bytes/text only; backend worker URL must be loopback; storage/provider targets are operator-controlled configuration or fixed HTTPS endpoints. |

Review fixes included separating assessment content versions, rejecting self-approval, adding actor foreign keys, gating incomplete indexes, suppressing internal assessments for ordinary users, queuing protected production email, excluding local secrets from publish, bounding image decoding, adding DPAPI/proxy trust, and replacing vulnerable dependencies. No known critical/high source finding was deliberately left unfixed; external verification gates remain in ACCEPTANCE.md.

Test review: fixtures use real PostgreSQL and HTTP authentication where relevant. Provider/encoder substitutes are confined to explicit external boundaries. The pgvector test's synthetic vectors do not establish AI quality. Existing frontend validation assertions were preserved when updating fixtures for server permissions/versions.

Documentation review: setup commands, config names, route paths, DTOs and status rules were checked against the implemented sources and generated OpenAPI. The old frontend completion report is labelled historical. Unexecuted deployment and inference claims are explicitly blocked.

clean-code-guard: reviewed; fixes above applied. Remaining limitation: paged report/item DTO assembly performs additional reads per record; validate query/load behavior before a larger deployment. No performance or production-readiness claim is made.

clean-code-guard: 11 fixed, 1 flagged for author (paged DTO query/load behavior).
# Deployment continuation review

Security-sensitive: YES. Reviewed directly by Codex against all ten categories in the security-review checklist. This is source/test evidence, not a penetration-test certificate.

- HTTPS mail uses a fixed provider endpoint, protected server credentials, bounded response parsing, stable outbox idempotency keys and sanitized failure codes. No provider bodies or email links are logged. Missing hosted sender configuration fails validation; local mail remains Development-only.
- Hosted startup rejects HTTP/loopback origins, wildcard/mismatched hosts, local media, local mail sinks and database connections without VerifyFull TLS. Keys persist outside the image and support PFX encryption. The independent-provider restart test verifies decryption with the persisted encrypted ring. Host filesystem/secret permissions remain deployment checks.
- Backup scripts pass argument arrays without a shell, stream dump plaintext directly into encryption, exclude repository destinations and retain failed files with an incomplete suffix. Restore verifies the ciphertext hash/size and refuses remote, non-test or nonempty targets. Only trusted operator-controlled archives may be restored; dumps contain executable definitions. Media backup, scheduling and off-site recovery are not yet implemented.
- CI uses read-only repository permissions, pinned official action commits, isolated local databases and no live application credentials. Reports contain synthetic CI data. Full-history Gitleaks is configured but not yet executed; a local release download timed out. A limited source/history scan found no known local credentials or selected provider-token patterns across 150 files and 265 Git objects.
- 67 backend cases passed after final mail changes, plus five backup safety tests. Dependency versions were unchanged in this continuation; the previous advisory evidence is historical until CI reruns it. No critical/high finding was identified in these changes. Hosted permissions, real mail/provider access, full inference, performance and complete restore remain release gates.

The original local implementation review is above.

## Current release/quota/privacy review

Security-sensitive: YES. Reviewed by Codex using security-review (10/10 checklist categories), clean-code-guard, test-guard and docs-guard. No new dependency was added. Parameterized database operations and shared mutation locks remain in place; React receives operational counts without private job payloads.

| Finding | Severity | Resolution |
|---|---|---|
| Existing hosted authenticated storage SELECT bypasses private matching for new prefix objects | HIGH external deployment gate | Guarded policy correction prepared; isolated proof pending CI; hosted application uploads/deployment blocked until reviewed approval and correction. Legacy objects/trials untouched. |
| Global inference permission alone could forward arbitrary report content | HIGH, FIXED in source | Scoped staff must explicitly approve each content version and separately approve the exact image. Owner edits invalidate approval; stale results cannot populate the new version. Six PostgreSQL-backed privacy scenarios pass. |
| Unbounded application inference attempts could exhaust free quota | FIXED | Transactional UTC minute/day reservations before network requests; concurrency/reset/rollback tests pass; NVIDIA disabled without explicit daily allowance. No automatic paid fallback. |
| Account access must match project-wide university-domain eligibility | REVIEWED | Deployment templates now use `Release:Mode=university`: exact `@o6u.edu.eg` only, mandatory verification before sign-in, and no self-service role grants. The optional `controlled` allowlist remains available for deployments explicitly requiring restricted tests. |

Backup subprocesses use argument arrays and scrub rclone environment overrides; encrypted source/filename copies preserve historical versions. Hosted credentials, destination permissions and complete recovery are unverified. CI has read-only GitHub permission and no live credentials; full-history Gitleaks passed on the prior commit. A limited current scan found no known local credentials/selected provider-token patterns across 177 worktree files and 412 history objects, which does not replace the next CI scan.

The current 90 backend tests and 47 frontend cases passed; provider test doubles remain confined to explicit network boundaries. No fake provider/model fallback was added to production. Cairo dashboard aggregation now runs in PostgreSQL and pending-claim filtering preserves department/date authorization. Paged report/item DTO assembly still needs load/query work before capacity claims. Documentation was checked against actual routes, templates and generated OpenAPI; unexecuted hosting/inference/recovery claims stay blocked.

CI run 42683987364 reported one Gitleaks false positive: `AI__OpenRouterKey=` followed by the empty `AI__NvidiaKey=` setting. The generic rule crossed the newline and extracted a configuration name. Reviewed exact commit/file/rule/line fingerprint is the only exception in `.gitleaksignore`; every new commit value and all other findings remain scanned. Empty template values are quoted to prevent recurrence. Actual pinned-model CI 42683987444 passed; calibration and public-host evidence remain separate gates.


Restore-verifier follow-up: canonicalization applies only to arrays composed entirely of string literals cast to unbounded varchar/text. It does not strip arbitrary SQL, column expressions or bounded-length casts. Regression cases prove changed allowed values remain failures. Real source/restored row bytes still compare exactly. Local isolated restored-copy verification passes; hosted/complete recovery is not inferred.

The custody panel now follows API canReceive/canHandover/canCancelHandover values, including explicit false, instead of gating receipt on a single primary department. Backward fixture fallback never grants backend access. Two regression variants cover secondary-department permission and authoritative denial; all 49 frontend tests and typecheck/lint/build pass.

Session/rate review: GET auth reads bypass only the named credential-attempt limiter and retain the global IP limiter. Credential mutations keep the same defaults and the new real API regression confirms two permitted failures followed by HTTP 429 under a two-attempt policy. Session-generation checks prevent stale startup responses from overwriting newer account state or reviving expired sessions. Frontend restoration no longer repeats on route changes; three lifecycle regressions pass. CI rate-limit overrides are isolated workflow configuration, not production defaults.

Cookie-renewal race review: renewing a session now executes a single UPDATE conditioned on existing unexpired idle/absolute limits, with expiry clamped to the stored absolute deadline. Concurrent logout/revocation deleting the row yields zero updates rather than a response-start exception or resurrection. A real PostgreSQL lock barrier deterministically exercises deletion versus renewal. The browser completion wait strengthens the scenario without replacing the independent race regression. No session duration or mutation-auth policy was weakened.

Resend evidence review: the shared test sender requires an exact configured university recipient before HTTP; the key remains in the launcher's hidden-input process environment. The provider endpoint is fixed HTTPS, error bodies are excluded, retry keys are stable and response reads bounded. Persisted provider ID/SentAt mean acceptance only; rejected sends retain a sanitized code and encrypted payload. Real PostgreSQL tests verify acceptance persistence, duplicate suppression after commit, rejection metadata and payload handling. No real delivery or user verification is claimed.

Recovery-list query review: inner joins use enforced item/requester foreign keys; authorization remains in SQL before paging, only requester display name is selected from Identity, and the same response projector retains private collection fields and authoritative actions. AsNoTracking is confined to the read list; mutation locking/version/audit paths are unchanged. Real PostgreSQL regression verifies constant command count and scope/privacy behavior; full backend 98 passed. Maintenance and found-item list N+1 paths remain open.

Maintenance batch review: authorized reports are selected before related reads, staff report IDs/departments bound assessment/assignee queries, version-pair checks exclude stale assessments, and explicit DTO projection strips DecisionJson/object keys while limiting assessment explanations to staff-scoped records. Tests include a staff-owned out-of-scope report to prove mixed actor roles cannot leak internal fields. One context is awaited sequentially; tracking changes apply only to read paths. Full 99-case PostgreSQL suite passed. No new provider, schema, credentials or permissions were introduced.

## 12 November non-AI release review

Security-sensitive: YES. Reviewed against all ten OWASP categories for the changed registration, email routing, lost-item visibility, and administrator demo setup paths.

- Registration checks the exact university domain by the existing domain validator, requires verification before sign-in, and leaves an explicit controlled-mode override available. The Resend test-recipient restriction applies only to the shared `@resend.dev` sender, so an owner-only acceptance mailbox does not constrain verified-sender delivery to other campus users.
- Demo setup endpoints require an authenticated administrator; POST remains under global antiforgery protection. The feature is disabled unless the host explicitly sets `Demo:PreparationEnabled`. No hosted settings were changed.
- New demo passwords are generated randomly, returned only on first creation, and never persisted in a separate credential store. Existing accounts are modified only if they carry the demo marker. User and role creation, demo records, grants, and audit event share a database transaction; uploaded media is cleaned up on failure and the exception propagates.
- Found-item listing exposes only available items with a live item-specific grant, and existing per-item/media access checks still enforce scope. Tests verify the requester sees the two granted records and an unrelated user cannot read those records or their private images.
- Database access is through EF parameterized queries; no dependencies, schema changes, secrets, or browser keys were introduced. React renders returned user-facing strings through normal text rendering. No XML parsing or new deserialization boundary was added. Sensitive provider errors remain sanitized.
- PostgreSQL integration coverage: 178 passed. GitHub CI for commit `833ba2c1e7ebf8c95a47509c09d6cd3c92e15ab4` passed, including history scanning, PostgreSQL 17 tests, storage-policy fixtures, isolated backup/restore, dependency checks, and Chromium/Firefox/WebKit journeys. A later favicon-only commit `ee1f5c2ea6c1f56291573e645314cc99f0e0568b` also passed CI and deployed successfully.

Remaining operational gates: the live `Release:Mode` value, Brevo sender/domain status and received-message authentication headers were not verified; no test message was sent. No full encrypted backup of the hosted database was created, no demo accounts were added, and demo preparation remains disabled. The university DNS administrator must publish any required Brevo authentication records. These are not covered by source-level security checks.

## 12 November maintenance AI review UI

Security-sensitive: YES. Reviewed the changed API response projection, existing review endpoint and worker path, and React submission/rendering against the ten OWASP categories. The API loads assessments only for reports whose current actor has administrator or matching maintenance scope; the added explanation stays inside that staff-only projection. The POST remains authenticated, CSRF-protected, version-checked, and requires `ConfirmedNonSensitive`; image forwarding is separately opt-in and server-gated. React renders provider text as escaped text and adds no HTML sink or external key. No dependencies or database schema changed. Frontend acceptance tests pass; PostgreSQL privacy integration `Reviewed_analysis_stays_private_and_never_sets_staff_decision` was not executed locally because no isolated PostgreSQL test database was configured. No critical/high issue found in the changed code. Live provider, calibration, CI and deployment gates remain open.

The broad default suggestion vocabulary was added to version-controlled configuration; deployment environment values retain precedence. It supplies only strict output labels and never toggles provider access, quotas, image forwarding, or search privacy/calibration gates.

## OpenRouter embedding collection-policy gate — 13 November 2026

Files reviewed: `RemoteEmbeddings.cs`, `ProviderTests.cs`, `RemoteEmbeddingsTests.cs`, `smartcampus.defaults.json`, `appsettings.Example.json`, and the hosted-model policy documentation.

| OWASP area | Result | Review note |
|---|---|---|
| A01 Access control / A04 design | PASS | Provider call requires external-processing approval and an explicit collection-policy opt-in; image transfer has its separate approval. Retrieval calibration remains fail-closed. |
| A02 Sensitive data / A09 logging | PASS with external approval caveat | API token stays server-side in the Authorization header; no key, provider response, prompt, image, or vector is logged. Default collection policy is deny. |
| A03 Injection / A10 SSRF | PASS | Fixed OpenRouter endpoint; caller cannot supply model, endpoint, or remote image URL. Text/JSON remains request data and images are resized/re-encoded before transfer. |
| A05 Misconfiguration | PASS | Missing or deny data-collection policy stops before quota reservation and network calls. Zero price cap and disabled fallback remain on the request. |
| A06 Dependencies | No dependency changes | Provider-policy gate adds no package. |
| A07/A08 | No relevant change | Authentication/token handling and persisted-data deserialization are unchanged. |

Live synthetic-only evidence: `deny` returned 404; pinned free-model text and image passage embeddings with `allow` returned normalized 1024-vectors. OpenRouter defines `deny` as excluding data-collecting providers, and the NVIDIA free model endpoint publishes logging/data-improvement terms. Production remains deny until university governance explicitly approves this data handling. No real user data was sent. `dotnet list backend/SmartCampus.slnx package --vulnerable --include-transitive` found no vulnerable packages in the solution; `npm audit --omit=dev` found zero vulnerabilities. No critical/high security finding was introduced; deployment approval remains outstanding.

## 13 November 2026 — AI Evaluation API and persisted results

**Security-sensitive:** Yes · **Reviewed by:** Codex · **OWASP categories reviewed:** 10/10 · **Status:** Pass

| OWASP area | Review result |
|---|---|
| A01 Broken access control | Controller requires authenticated admin on every endpoint; isolated test verifies maintenance staff receives 403. Run IDs are only resolved in admin-only handlers. |
| A02 Sensitive data | Every prompt/image is a server-authored fictional fixture. Provider key is read server-side and is never serialized. Production user-data approval flags are not changed. |
| A03 Injection | Suite is a fixed whitelist; EF Core queries parameterize GUIDs and keys. CSV cells are quoted and formula-prefixed values are escaped. React renders returned values as text. |
| A04 Insecure design | Cost ceiling is zero, fallback is disabled, fixed free model IDs are validated by existing adapters, requests are sequential and budgeted, and completed cases are persisted before any next request. |
| A05 Misconfiguration | No startup migration was added. Only the reviewed explicit additive migration was applied. New tables are in the private `smart_campus` schema. |
| A06 Vulnerable components | No dependencies were added; `npm audit --audit-level=moderate` found zero vulnerabilities. |
| A07 Identification/authentication | Existing ASP.NET authentication and authorization are reused; no credential/session code changed. Mutation routes use the application's global antiforgery policy. |
| A08 Integrity failures | Caller cannot supply prompts, images, endpoints or model IDs; JSON is parsed by typed System.Text.Json; invalid provider values fail through existing validators. |
| A09 Logging/monitoring | New handlers do not log prompts, images, provider keys or raw provider bodies. Per-case sanitized error code and elapsed time are persisted for admin review. |
| A10 SSRF/unsafe requests | Provider destination and model routing come from existing server adapters; no client-controlled URL or redirect target is accepted. |

The expired-run path marks an in-flight case result unknown and never retries it automatically. Explicit resume selects only pending cases. Full live provider tests and public-browser checks are deployment acceptance tests, not security-test evidence.

## 13 November 2026 — Found-item custody projection

**Security-sensitive:** Yes · **Reviewed by:** Codex · **OWASP categories reviewed:** 10/10 · **Status:** No critical/high finding in source review.

The found-item response now redacts `Storage` unless the actor is the current custodian or an administrator. Handover office/status are returned only to the current custodian, administrator, or staff scoped to the receiving department. Search-grant holders and claim requesters still receive only permitted candidate/item fields; their media access continues through the per-item authorization check. The React detail view hides the custody-location section for potential claimants. Database regression assertions cover holder, receiving department, match requester, existing claimant, outsider and admin projections, but were not executed in the latest run because isolated PostgreSQL was unavailable. A frontend regression verifies the sensitive location is not rendered for a potential claimant.

OWASP checks: A01 authorization predicates and role/scope boundaries remain server enforced; A02 storage/office fields are redacted; A03 projection values are not interpolated into SQL; A04 similarity still does not prove ownership; A05 no hosting/schema configuration changed; A06 no dependency changes (`npm audit --omit=dev --audit-level=moderate`: 0 vulnerabilities; `dotnet list ... --vulnerable --include-transitive`: no vulnerable packages from configured sources); A07 identity/session code is unchanged; A08 returned values remain typed DTO/JSON and React text; A09 no new sensitive logging; A10 no new server-side request surface. No hosted database or live user data was accessed.

## 13 November 2026 — Local continuation security review

**Security-sensitive:** Yes · **Reviewer:** Codex · **OWASP categories checked:** 10 · **Scope:** changed AI routing, diagnostics, search indexing, admin/demo routes and frontend integration. Source review only; no penetration test or live provider request.

| OWASP area | Review result |
|---|---|
| A01 Access control | Search remains behind the existing account route and server-side `Access`/eligible-item filtering. Admin operations remain authorized. Demo routes were removed; historical evaluation API paths return 404 outside Development. Database-backed access tests were among the 50 tests skipped without an isolated database. |
| A02 Sensitive data | OpenRouter key remains server configuration only. Per-report and per-image consent gates were preserved. Search errors do not contain prompts, images, provider responses or keys. No real data was submitted externally. |
| A03 Injection | New vector-presence query uses Npgsql parameters for source ID, revision and model. Existing score SQL still parameterizes vectors, scopes and threshold. React outputs are rendered as text. |
| A04 Insecure design | Classification uses one pinned free model, `max_price` zero and `allow_fallbacks=false`; legacy routing rejects instead of falling back. Persisted current-revision vectors are reused on requeued indexing work. |
| A05 Misconfiguration | No migration, production setting, database, or deployment was changed. Unknown API paths return 404 rather than the SPA shell. Historical evaluation data remains untouched. |
| A06 Dependencies | `npm audit --omit=dev --audit-level=moderate`: 0 vulnerabilities. `dotnet list backend/SmartCampus.slnx package --vulnerable --include-transitive`: no vulnerable packages in the current configured sources. |
| A07 Authentication | No credential/session implementation changed; existing account and staff approval gates remain. The full database-backed authorization suite was not runnable locally. |
| A08 Integrity | Provider response model ID and label allowlists remain validated; model revision is part of vector lookup. No client-supplied model/endpoint was added. |
| A09 Logging | The operations page is read-only and contains no test actions. No provider output or secret logging was added. |
| A10 SSRF | Fixed OpenRouter HTTPS URLs remain server-owned. No user-controlled URL is introduced. |

**Known verification limit at the time of this review:** 127 tests passed, 50 database-dependent tests skipped because no isolated PostgreSQL test DB was available. Brave visual inspection was stopped, and no live provider request was executed. This review is not a full-project security certification.

## 13 November 2026 — AI Evaluation runtime grants

**Security-sensitive:** Yes · **Reviewed by:** Codex · **OWASP categories reviewed:** 10/10 · **Status:** Pass

The live error log showed SQLSTATE `42501` on `smart_campus.AiEvaluationRuns`. The production app role is `smart_campus_runtime`; it already had DML rights on existing application tables. The new private-schema evaluation tables were owner-only after the additive schema migration. Follow-up migration `20261113023724_AiEvaluationRuntimePermissions` grants only `SELECT`, `INSERT`, and `UPDATE` on the three evaluation tables to that exact runtime role, conditionally when it exists. It grants no DELETE, no `PUBLIC`/Data API roles, creates no RLS bypass, and changes no existing table ACL. Down migration revokes only the same three privileges. Production `has_table_privilege` checks confirmed the grant set and the new tables remain empty. Supabase security/performance/health advisors were checked; no new finding applied to this private-schema permission change.

## Local continuation review — 13 November 2026 (unreleased)

The full backend suite passed 177/177 against an isolated, disposable PostgreSQL 16 + pgvector database. The added frontend failure-state regression passed as part of 67/67 Vitest cases; TypeScript, ESLint and production build passed. `npm audit --omit=dev --audit-level=moderate` reported zero production dependency vulnerabilities. Manual Brave review covered public home and auth pages, including mobile/tablet/desktop sizes and both languages. The local frontend's `/api/v1/auth/me` request returned 500 because no API server was running on local port 5080; a bilingual session-status notice now reports this instead of implying that session restoration succeeded. Authenticated staff/admin workflows, the full visual matrix, live OpenRouter calls and production acceptance remain unverified. No production secrets, records, settings, mail, migrations or hosting state were changed. This remains a source/test review, not a complete security certification.

## Follow-up verification — 13 November 2026

After correcting the isolated test connection to use its dedicated local role and `_test` database, the complete PostgreSQL-backed suite passed 177/177. A preceding run used the wrong credentials and failed before database setup; it changed no test schema and is not counted as an application regression. Frontend automation passed 68/68, plus typecheck, lint and production build. The map-position editor adds keyboard access while preserving server-side admin authorization and version checks. This follow-up deliberately did not use Brave or make real OpenRouter requests. Provider privacy approval and retrieval calibration remain unresolved gates; no claim of end-to-end or complete security acceptance is made.
### Department notification recipient review — 13 November 2026

- **Security-sensitive:** Yes — account state and department-scoped notification recipients.
- **OWASP categories checked:** 10/10 for the changed path.
- **Finding fixed:** notifications for handovers and department-held items now exclude inactive or email-unconfirmed accounts, while retaining the existing role and department-scope checks.
- **Injection/data exposure:** EF Core LINQ remains parameterized; notifications contain only the authorized entity reference and event type; no credentials or provider secrets are involved.
- **Persistence/permissions:** no schema or grants changed. Integration regression added; execution is deferred because this environment has no isolated PostgreSQL test server.
- **Residual risk:** runtime behavior of these two database-backed notification paths awaits the isolated integration test run.

## Maintenance AI provider collection-policy gate — 13 November 2026

**Security-sensitive:** Yes · **Reviewed by:** Codex · **OWASP categories checked:** 10/10 · **Status:** Pass with external governance gate retained

| OWASP area | Review |
|---|---|
| A01 Broken access control | No identity, role, or resource-scope changes. Report-level user confirmation and staff-only review remain enforced by the assessment workflow. |
| A02 Sensitive data | The free Super VL provider discloses logging and warns against personal/confidential inputs. New `AI:ProviderDataCollection` must explicitly equal `allow`; absent/deny blocks before budget reservation or HTTP. Defaults and example config remain `deny`. |
| A03 Injection | Fixed OpenRouter URL and model remain server-owned; request JSON is serialized, never string-concatenated. |
| A04 Insecure design | Request keeps `allow_fallbacks=false`, zero prompt/completion price caps, and validates response model/taxonomy. Provider-collection approval is distinct from the user/staff review gates. |
| A05 Security misconfiguration | No production environment value was changed. Admin operations shows only the boolean approval state; no key is returned. |
| A06 Vulnerable components | No dependencies added. Current `npm audit --omit=dev`: 0 vulnerabilities; NuGet `--vulnerable --include-transitive`: no vulnerable packages in configured sources. |
| A07 Authentication | No sign-in/session/token code changed. Existing authenticated staff review and admin authorization remain. |
| A08 Data integrity | Provider response JSON parsing, exact free model validation and taxonomy allowlists remain unchanged. |
| A09 Logging/monitoring | No API key, prompt, image, provider response body, or exception detail is logged. |
| A10 SSRF | The request destination remains the fixed OpenRouter HTTPS endpoint; no user-controlled URL was added. |

`ProviderTests` passed 26/26 after the provider-policy change. Full backend suite then passed 136 with 55 database-dependent tests skipped because `SMART_CAMPUS_TEST_DB` was not configured. The subsequent isolated PostgreSQL run passed 200 with one opt-in live-provider test skipped. One earlier key attempt returned HTTP 401; it was superseded by the replacement-key application-path test, which passed six bounded fictional calls. No real student input, production key/settings, or production data was used. This source review is not a penetration test or full-project security certification.
# University-domain account eligibility — configuration review (13 November 2026)

- Scope: deployment templates changed from `Release:Mode=controlled` with a single-email allowlist to `Release:Mode=university`, matching the explicit requirement that any exact `@o6u.edu.eg` address can register. No MonsterASP runtime value was read or modified.
- Access control: `AuthController.Register` normalizes and validates the exact university domain before consulting `ReleasePolicy`; `university` mode permits that domain only. Email confirmation remains mandatory before login, and registration does not grant any staff or administrator role. Existing server-side role/scope checks remain unchanged. `controlled` remains available for deployments explicitly requiring a restricted test list.
- Privacy/operations boundary: this setting enables university-domain accounts only; it does not approve external AI data transfer or authorize processing real student information on MonsterASP Free. Those restrictions remain in the deployment guidance and AI defaults.
- Findings: no code authorization bypass introduced. Live host configuration and real mailbox delivery were not verified; the external settings remain untouched. A deployment must use a verified transactional sender capable of delivering beyond a provider's shared test-recipient restriction.

## Gemini Embedding 2 migration — 13 November 2026 (local/unreleased)

**Security-sensitive:** Yes · **Review:** source-level checklist complete · **Disposition:** pass with external policy and billing gates retained

| OWASP area | Review |
|---|---|
| A01 Broken access control | No authentication or role changes. Search continues to exclude the requester’s own uploads/current custody, filters department scope in SQL, and grants only returned candidates. Retrieval tests cover scoped visibility and uncalibrated fail-closed behavior. |
| A02 Sensitive data exposure | Gemini key is read only from server configuration and sent in `x-goog-api-key`; it is not placed in a URL, request log, or API response. Separate text, image, provider-collection and global transfer approvals remain required and default false/deny. Free-tier data handling still needs university approval before real records/images are sent. |
| A03 Injection | Reindex/search SQL uses Npgsql parameters for values. The pgvector extension namespace is quoted before being inserted as a SQL type/operator identifier. Request JSON is serializer-generated. |
| A04 Insecure design | Exact model/endpoint and revision are pinned; no provider fallback or retry exists. Daily/minute reservations precede uncached requests, cache hits avoid quota spend, and vector dimensions/norm are validated. `GeminiFreeTierOnly` is an operator assertion, not an API-enforced billing check; zero-cost operation depends on Google project billing remaining disabled. |
| A05 Security misconfiguration | Default/example production approvals and reindex flag remain off. Calibration revision and both modality thresholds must match before search opens. Reindex batch size is clamped to 1–100. |
| A06 Vulnerable components | No package was added by this migration; SkiaSharp was already referenced. Release tests exercise supported image formats, pixel and byte caps, resizing, and malformed input. |
| A07 Authentication | No password, token, cookie, or role behavior changed. |
| A08 Data integrity | No database schema or existing data was modified. New vectors are separated by model/revision/dimension; invalid, non-finite, or non-normalized vectors are rejected. Existing score calibration cannot authorize the new vector space. |
| A09 Logging/monitoring | Gemini API error bodies, request bodies/images, and key are not logged. Transport errors are reduced to stable application codes. |
| A10 SSRF | Destination is the fixed Google HTTPS endpoint; request URL is not user-controlled. |

Local evidence: full backend Release suite **211 passed, 3 opt-in skips, 0 failed**; focused reindex tests **2/2 passed**; frontend **82/82 passed**, typecheck, lint and production build passed. No live Gemini request was made in this pass at the user's request, so provider availability, actual free-tier billing behavior, and retrieval accuracy remain unverified. No production settings or data were changed.

## Image-only search follow-up — 14 November 2026 (local/unreleased)

Input validation now requires a description or photo, with a 2,000-character query cap and the shared 5 MiB image cap. A malformed photo fails before any text inference request. Image-only queries have an independent default-unset calibration threshold; no old combined score authorizes them. Query SQL filters the exact model/revision/dimension, uses parameterized values, and retains ownership/custody/department exclusions and candidate-only grants. Missing eligible photo vectors cause a fail-closed response. Indexing checks both generic and Google-specific photo approval before reading/sending private media. No migration, secret, provider fallback, retry, or authentication change was introduced.

Verification: isolated PostgreSQL Release suite 216 passed, 3 explicit opt-in skips, zero failed; frontend 92/92, typecheck, lint and production build passed. The separate owner-run live Gemini text/image integration test succeeded with 768-dimensional results, but neither synthetic vectors nor that integration test establish calibrated retrieval quality. Production privacy, billing, calibration and hosted acceptance remain external gates.

The subsequent worker-recovery change enforces the three-attempt limit even after an expired running lease is reclaimed following restart. Completed assessment output is preserved; exhausted incomplete work becomes unavailable without another provider dispatch. The row lock, availability lease and atomic job/assessment save remain in place. No authentication, schema, secret, paid fallback or retry-limit increase was introduced. The upload UI now cancels stale asynchronous selections and keeps submission blocked during validation. Final backend verification: 218 passed, 3 opt-in skips, zero failed; frontend: 94/94, typecheck, lint and production build passed. No real provider call was made for these changes.

## Explicit Gemini reindex generation retry — 14 November 2026 (local/unreleased)

**Security-sensitive:** Yes · **OWASP review:** complete for the changed provider queue path · **Disposition:** no new issue found; external approvals remain required

| OWASP area | Review |
|---|---|
| A01 Broken access control | No endpoint or user privilege was added. Reindex scheduling remains a backend hosted service gated by the existing explicit operator approvals. |
| A02 Sensitive data exposure | The generation number contains no user data or secret. It does not change which fields may be sent to Gemini; text/image consent, provider collection and free-tier assertions are unchanged. |
| A03 Injection | Generation-derived suffix is an integer clamped to 0–1,000,000; SQL comparison is parameterized. Job keys remain application-generated. |
| A04 Insecure design | A failed job is not retried automatically after three attempts. A deliberate new generation may schedule one bounded job; indexing reuses vectors already stored for the active revision and still reserves budget before uncached provider requests. |
| A05 Security misconfiguration | Generation defaults to 0; all external-transfer and reindex approvals remain false/deny in defaults and examples. No host setting was changed. |
| A06 Vulnerable components | No package/dependency change. |
| A07 Authentication | No account, cookie, token, password, or role code changed. |
| A08 Data integrity | Each generation has a distinct unique job key. Existing terminal job and partial vectors remain intact; `ON CONFLICT` vector identity remains source+revision. |
| A09 Logging/monitoring | No provider payload, vector, API key, or config values are logged by the generation path. |
| A10 SSRF | No request URL or network destination was changed; provider URL remains pinned in `RemoteEmbeddings`. |

Focused reindex tests passed **3/3** and the full isolated PostgreSQL Release suite passed **224/224 executed, 3 opt-in skipped, 0 failed**. No external API call or production setting change occurred. This source-level review does not establish Google billing status, privacy approval, or retrieval accuracy.

## Large pagination input handling — 14 November 2026 (local/unreleased)

**Security-sensitive:** Yes · **Review:** input and query boundary reviewed · **Disposition:** pass

The shared `Rules.PageWindow` clamps the page size, normalizes non-positive pages, computes offsets in 64-bit arithmetic, and bounds them to the maximum offset accepted by EF. User input remains a numeric query parameter; it is not interpolated into SQL. The helper is used by maintenance, found-item, claim, notification and admin-account lists. No authorization filter, selected row scope, or returned contract changed.

OWASP check: A01 permissions stay server-side and unchanged; A03 arithmetic cannot become a negative/overflowed query offset and SQL remains parameterized; A05 input bounds are centralized; A06 no dependency change; A07 no authentication change; A08 ordering/pagination data remain unchanged except that an out-of-range maximum page returns an empty list. No secret or personal data is logged. Focused pagination tests passed **6/6** and the full isolated PostgreSQL Release suite passed **225 passed, 3 opt-in skips, 0 failed**. This is a targeted source review, not a penetration test or whole-site certification.

## Maintenance provider image normalization — 14 November 2026 (local/unreleased)

The image passed to OpenRouter now uses the same fixed decoder/normalizer as stored uploads and Gemini image requests. The decoder accepts only JPEG/PNG/WebP content, verifies pixel count before decode, removes metadata by re-encoding pixels, and caps normalized output at 1.9 MB/2048 pixels. Validation and normalization occur before budget reservation; the server still requires its existing non-sensitive-image, report review and provider-collection approvals. The model/URL are unchanged; no provider fallback or extra request was introduced.

OWASP review: A01 authorizations unchanged; A02 image sharing remains opt-in and metadata is stripped; A03 no untrusted image bytes are forwarded without decoder validation; A04 shared limits reduce inconsistent resource exposure; A05 bounded upload and provider payload limits remain; A06 no dependency added; A07 authentication unchanged; A08 invalid images do not create AI suggestions or reserve budget; A09 no images/key/provider response are logged; A10 no destination change. `ProviderTests` passed **46/46** and the full isolated PostgreSQL Release suite passed **226 passed, 3 opt-in skips, 0 failed**. No live provider request or production change occurred.
