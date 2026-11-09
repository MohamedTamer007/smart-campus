# Architecture and decisions

React calls one ASP.NET Core 10 controller API. Domain entities/rules live in SmartCampus.Domain. EF, Identity, session tickets, transactional services, private media, email and provider adapters live in SmartCampus.Infrastructure. Controllers bind inputs and publish response contracts. No generic repository, mediator or distributed bus is introduced.

EF owns private smart_campus tables. Identity is independent of legacy Supabase Auth. Existing public tables, private buckets and historical trial records remain untouched. Migrations are explicit operator actions, never startup side effects. Use distinct migration DDL and runtime DML roles; only the backend holds privileged storage/provider configuration.

Maintenance uses row locks and optimistic versions. Separate content versions identify assessment inputs. Approvals, handovers and delivery confirmations lock the same item row. Partial unique indexes enforce one reservation, one active requester claim and one open handover. Custodian XOR checks, foreign keys, history and transactional notifications preserve integrity. The final actor-integrity migration was applied after WSL recovery and exercised by the final database suite.

Cookie handles are random, hashed at rest, revocable and independently bounded by idle/absolute expiry. Tickets and mail outbox payloads are encrypted with persisted data-protection keys. Windows uses DPAPI for newly generated keys; Linux deployments must protect the persistent key directory. Current grants are read on each request.

Background jobs use leases, SKIP LOCKED, unique input-version keys and at most three attempts. SMTP delivery is at-least-once: a crash after SMTP acceptance can repeat a message. Failed/unconfigured jobs remain visibly unavailable, never fabricated successes.

The private SigLIP worker accepts text/image bytes on loopback, not paths or URLs. pgvector stores actual dimensions and revision metadata. Initial retrieval uses exact cosine ranking: text-to-image/finder-description weights 0.7/0.3; optional image similarity is averaged with that score. These are defaults requiring calibration, not measured quality claims. No fixed dimension or approximate index is invented. Actual pinned-model CPU inference passed in GitHub CI with 768-dimensional compatible text/image vectors. Local weight loading remains disk constrained. Calibration, ARM runtime and hosted latency remain unverified.

OpenRouter uses the selected Nemotron free model, zero-price caps and no automatic fallback. NVIDIA gpt-oss-20b is explicitly selected and text-only. Taxonomy/inference default disabled. Outputs must pass validation and remain staff suggestions. Each report content version requires explicit scoped staff approval before external forwarding; an image also requires separate approval of its exact attachment and global non-sensitive forwarding enablement.

Approved choices: at most five qualifying candidates; no new claims while reserved; current-custodian cancellation before either confirmation; reservation guards on transfers; ItemRecovered closure of competitors; separate maintenance cancellation. General search release is gated on reviewed calibration. Original agreed requirements remain authoritative otherwise.
