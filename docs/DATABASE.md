# Database and private media

EF Core migrations in `backend/SmartCampus.Infrastructure/Migrations` are authoritative. `database.sql` is the idempotent review export for the current migration history, including `AiEvaluationResults`. Hosted rollout is an explicit operator action; the application never automatically migrates at startup.

## Boundaries

The target is Supabase project `mninpcrhqsaafoshjqpn`, PostgreSQL 17. The existing public schema and trial records remain legacy data. New application/Identity tables live in `smart_campus`, outside the exposed Data API schemas. The API connects directly through Npgsql and enforces current account, role, scope, ownership and version checks.

`CampusDb` maps Identity users/roles, department grants and location hierarchy, maintenance reports/history/assessments, found items/handovers/recovery, private media metadata, notifications/audit, server sessions, search grants, mail outbox, inference-usage counters and durable jobs. Vector storage is installed by the explicit retrieval migration and records actual embedding dimensions/revisions.

Foreign keys preserve actor and domain relationships. Check/unique constraints enforce valid states, one custodian, one open handover, one active reservation, and one active request per requester/item. Item-affecting approval, custody and confirmation operations lock the same item row. Integer versions reject stale writes.

## Rollout

1. Inventory the actual target and export an authorized backup before changes. Review the generated SQL and reject destructive changes affecting existing data.
2. Rehearse on isolated PostgreSQL 17 with pgvector. Use migration credentials for the explicit `--migrate` command; startup does not perform this operation.
3. Provision a distinct runtime role with application DML and extension-schema usage, without ownership/DDL or legacy-table access. Use verified database TLS and a bounded connection pool.
4. Verify migration history, constraints, persistence and legacy-record counts afterward. Check Data API grants and storage policies with direct-access tests.

The local development role/database are synthetic. They are not hosted runtime credentials. CLI setup and connection-search-path requirements are in [SETUP_WINDOWS.md](SETUP_WINDOWS.md).

## Media and recovery

The private `maintenance-images` and `lost-found-images` buckets receive new `smart-campus/` object keys. Backend authorization gates every download; public object URLs are not persisted. Existing bucket objects remain intact.

Database dumps do not contain storage object bytes. Database and encrypted incremental media-copy tooling exist; hosted credentials, scheduling and complete recovery evidence remain pending. See [OPERATIONS.md](OPERATIONS.md).

The hosted legacy `campus_images_read` policy currently grants authenticated users SELECT across both application buckets. That would expose new application-prefix objects outside backend authorization. The reviewed operator script `scripts/sql/restrict-storage-prefix.sql` excludes `smart-campus/` while preserving legacy-prefix reads. It stops on unexpected policies or non-private buckets. It has not been applied to the hosted project and requires explicit permission review/approval. A synthetic isolated CI fixture is prepared to verify the intended rule; execution evidence is pending.

The seventh migration, `ReviewedAssessmentInputs`, adds the reviewer, review time and exact optional image identity to versioned assessments. Existing assessments remain unreviewed and cannot be forwarded automatically. The reviewer foreign key restricts deletion. The image identity is historical evidence rather than a foreign key to replaceable media.

The eleventh migration, `AiEvaluationResults`, adds three private-schema tables for synthetic evaluation runs, per-case results and revision-keyed embedding vectors. It does not alter existing application tables; its down migration removes only these evaluation tables. Apply it explicitly after reviewing `database.sql` and completing the operator's authorized backup procedure. The application does not migrate on startup.
