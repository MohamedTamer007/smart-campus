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
| Testing deployment could admit unapproved university accounts | FIXED | Hosted controlled allowlist; checked at login, private requests, verification/reset and queued delivery. Removal blocks an existing session. Recovery remains generic. |

Backup subprocesses use argument arrays and scrub rclone environment overrides; encrypted source/filename copies preserve historical versions. Hosted credentials, destination permissions and complete recovery are unverified. CI has read-only GitHub permission and no live credentials; full-history Gitleaks passed on the prior commit. A limited current scan found no known local credentials/selected provider-token patterns across 177 worktree files and 412 history objects, which does not replace the next CI scan.

The current 90 backend tests and 47 frontend cases passed; provider test doubles remain confined to explicit network boundaries. No fake provider/model fallback was added to production. Cairo dashboard aggregation now runs in PostgreSQL and pending-claim filtering preserves department/date authorization. Paged report/item DTO assembly still needs load/query work before capacity claims. Documentation was checked against actual routes, templates and generated OpenAPI; unexecuted hosting/inference/recovery claims stay blocked.

