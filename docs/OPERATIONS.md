# Operations and recovery

The public testing environment has not been provisioned. The agreed targets are RPO 24 hours, RTO four hours and ten simultaneous signed-in users with warm ordinary API p95 below two seconds. They are not verified service guarantees.

## Health and diagnosis

`/health/live` reports process liveness. `/health/ready` checks database connectivity only. Neither proves successful migrations, email delivery, media access, AI inference or calibrated retrieval. Detailed diagnostics must remain administrator-only.

Use structured endpoint/trace/job identifiers and failure codes. Do not record passwords, tokens, email links, private descriptions, image bytes or collection contact details. Inspect job attempts and availability privately. A provider HTTP acceptance is not proof of mailbox delivery.

Persist the Data Protection key directory and the configured PFX certificate across redeployments. The application encrypts keys with that certificate when configured. Hosted validation requires an explicit certificate path, HTTPS origin, exact allowed host, external media storage, real mail configuration and verified database TLS. File permissions must restrict key/certificate/configuration access to the service and authorized operators. Keep a separate recoverable copy of the wrapping certificate; losing it can invalidate sessions and encrypted mail payloads even when the database survives.

## Encrypted database backup tooling

Prerequisites: Python 3.11+, PostgreSQL client tools matching the server major version, and [age](https://age-encryption.org/). Install from official sources. Configure the source through protected libpq service/password files or environment injection. Keep credentials, private age identities and backups outside Git and published images.

```bash
python scripts/backup-database.py --output-dir /private/backups --recipient AGE_PUBLIC_RECIPIENT
```

The default scope exports `smart_campus` in PostgreSQL custom format directly into age encryption. Plaintext database dumps are not written. The script creates a ciphertext file and a manifest containing its hash/size. Failed jobs retain an `.incomplete` file and return nonzero; do not count that file as a backup.

An authorized pre-migration inventory backup can use `--scope full`. Full Supabase recovery has additional role/managed-schema requirements and must follow Supabase's current recovery guidance; the isolated restore helper intentionally accepts application backups only.

```bash
export PGHOST=127.0.0.1
export PGDATABASE=smart_campus_restore_test
python scripts/restore-database.py --manifest /private/backups/SNAPSHOT.manifest.json --identity-file /private/recovery/backup.key
```

Create the empty isolated database and required pgvector extension/schema first. Restore refuses remote targets, database names without `_restore_test`, nonempty targets, traversing archive names and mismatched archive hashes/sizes. It never runs `--clean` or drops a database. Use only trusted backups from controlled storage; a dump contains executable database definitions.

Five local safety tests passed. Actual encryption/restore is configured in CI but has not yet run. A successful restore must compare domain records, constraints, versions, migrations and authorized access, then separately restore/check media and protected keys. Do not call recovery verified based only on script existence.

## Pending operational setup

- Schedule encrypted backups every 12 hours and alert before backup age reaches 24 hours. Keep seven daily/four weekly recovery points within measured free storage/egress limits.
- Export private media bytes and an integrity manifest; use incremental object backups and test restoration without overwriting live objects.
- Upload encrypted backups to a private off-VM destination, with a narrowly scoped identity and recovery keys stored separately. Confirm free capacity before enabling schedules; never silently bill or discard required recovery points.
- Monitor resource usage, certificates, storage/database/provider quotas, job failures and delivery failures. Implement administrator-only dependency summaries and operational alerts.
- Deploy by immutable commit/image identity after checks pass. Record the previous version and verify rollback compatibility before switching. Prefer forward database corrections; destructive rollback needs approval.
- Measure restart, redeploy and complete isolated recovery against the agreed targets. These checks remain pending.
