# Live release gates — 2026-11-11

This is **not** a production sign-off. The existing running SnapDeploy container and Supabase project were inspected without destructive SQL, account elevation, hosting changes or paid services.

## Observed live data

Read-only Supabase checks on the existing private `smart_campus` schema returned: `AspNetUsers=1`, `AspNetRoles=0`, `Departments=0`, active receiving departments `=0`, `Locations=0`, `Scopes=0`, `Items=1`, `Media=1`, `Reports=0`, `Embeddings=0`. Job groups: two email jobs completed and one `index` job pending after two attempts. Storage metadata presence does not replace a public-site authenticated GET and persistence test.

The receiving-department dropdown correctly has no real entries because the API returns the `Departments` table filtered by `Active`, while React selects only `CanReceive` entries. A new UI change explains missing/failed directories rather than silently displaying only `Choose department`.

**Supabase advisory:** RLS is disabled for the 27 private application tables. This warrants a security review. The read-only grant checks confirmed `anon` and `authenticated` both lack USAGE on `smart_campus` and lack direct SELECT on `smart_campus."Items"` at inspection time. Do **not** claim the private schema is publicly accessible without verifying permissions, and do **not** blindly execute advisory RLS SQL; coordinate with the application role and test access control.

## Actual gates / ownership

| Priority | Gate | Required action | Current evidence |
|---|---|---|---|
| P0 | Admin bootstrap | Explicitly identify an already registered and verified administrator mailbox. Privately set `Bootstrap__Email`; use approved one-time secure `--bootstrap-admin` process with a backup and audit, then remove bootstrap secrets. Never infer the admin from a screenshot or arbitrary student identity. | Roles absent; no privileged role assigned |
| P0 | Official receiving & maintenance departments | University/team validates names, codes, which units receive custody (`CanReceive`) and which handle faults (`Maintenance`). Admin creates records through authenticated endpoints. | `Departments=0`; dropdown empty |
| P0 | Staff and university locations | Real staff enrol and verify with consent, admin grants exact `department`/`maintenance` scopes; team confirms faculty, buildings, floors and routing. No fictitious directory is acceptable. | `Locations=0`, `Scopes=0` |
| P0 | Password verification email delivery | Configure a domain/sender the owner can authorize and verify; test delivery and recovery into an actual `@o6u.edu.eg` inbox. Provider `Sent` alone is insufficient. | Two mail provider jobs completed; no independent delivery proof |
| P0 | Private media | Test authorized upload and read, unauthorized denial, persistence and backup/restore across container restart. | One metadata row and image observed, but no completed full media acceptance |
| P1 | Retrieval worker and calibration | Provision a sufficiently large secured SigLIP2 runtime. Existing API uses `http://127.0.0.1:8123/` **only**; external deployment requires code+transport security review. Run independently reviewed open-set evaluation before setting approved threshold; verify index retries. | No vectors; one index pending |
| P1 | Maintenance AI | Review taxonomy and acceptable data-sharing restrictions with institution. Privately configure chosen provider only if approved and available; test failure/quotas safely. | No actual hosted AI acceptance |
| P1 | Integration and recovery | Run real multi-account flows for maintenance, lost-item claim, custody, cancellation, notifications, reset, CSRF, session revocation, rate limiting, backups and restore. Use isolated test fixtures without endangering production records. | Local CI evidence exists but is not hosted end-to-end acceptance |
| P1 | Hosting sustainability | 512 MB SnapDeploy container auto-sleeps; validate idle wake, monthly hours, job execution and worker availability. No unapproved paid upgrade or 24/7 claim. | Hosting running, worker not deployed |

The source package with graduation-project diagrams documents planned workflows but does not supply independently verified department personnel, buildings, mailbox sending authorization or ownership of personal objects. The Drive dataset collection is for **offline evaluation**, never a public found-item inventory.

## Safe deployment order

1. Review the current branch and run GitHub CI. Do not merge to `main` or automatically redeploy the existing SnapDeploy container.
2. Obtain institution-approved directory/roles and authorized sender/recipient records.
3. Bootstrap administrator with explicit mailbox approval and transactionally backed-up operations.
4. Create official receiving/maintenance departments, locations and scoped staff via API and verify fresh browser sign-in.
5. Deploy and evaluate the AI worker separately; approve retrieval calibration only from independently reviewed real no-match and positive pairs.
6. Perform secure live acceptance with two or more legitimate test actors; test backup recovery and operational controls.
7. Obtain explicit owner/institution approval before opening `Release__Mode=open` or claiming production readiness.

No items, users, roles, public tables, bucket permissions or deployment resources were mutated as part of this checklist.
