# 12 November release alignment

This matrix maps the approved non-AI release request to code and recorded acceptance evidence. The 18 design boards in the supplied project document were reviewed as visual references. They contain illustrative data; this implementation does not present illustrative values as live operational records. AI analysis and matching remain deferred.

| Requirement | Implementation | Acceptance evidence / current state |
|---|---|---|
| Arabic/English visual direction, original campus image, university mark | `src/app/App.tsx`, `src/app/Home.tsx`, `src/styles/app.css`, `src/assets/campus-photo.webp`, `public/o6u-mark.png` | Historical automated run: Chromium and WebKit passed 28/28 at 390/768/1440 for the covered public and guest flows, in both directions. Firefox could not launch on this Windows host (`spawn UNKNOWN`). Browser validation was not repeated in this continuation per user direction; full visual parity remains unverified. |
| Locally served Noto Sans Arabic and Nunito Sans | `src/assets/fonts/`, `src/styles/app.css` | Production bundle contains local WOFF2 assets and SIL OFL notices; no Google Fonts import. |
| All exact `@o6u.edu.eg` accounts can register and receive transactional messages | `ReleasePolicy.cs`, `AuthController.cs`, `EmailSender.cs`, `ResendEmailTransport.cs`, deployment templates | Local defaults and deployment templates use `university`; any exact domain address is accepted, with email confirmation required before sign-in and no privileged role granted by registration. `controlled` remains an optional restricted-test mode. Production MonsterASP environment was not inspected or changed; actual provider delivery to university recipients remains unverified. |
| Account verification and recovery email matching site brand | `CampusEmailContent.cs`, `BrevoEmailTransport.cs` | Automated tests cover branded HTML, safe action links, text fallback and recipient routing. A real message to the approved test mailbox `23012888@o6u.edu.eg`, Brevo event, authentication headers and inbox placement remain unverified. |
| Maintenance submission, review and tracking | `src/features/maintenance/Maintenance.tsx`, backend maintenance service/controller | Latest full backend suite passed against isolated PostgreSQL 16 + pgvector: 201 passed, 0 failed, 1 intentional live-provider opt-in skip. Hosted account walkthrough remains outstanding. |
| Found-item registration, custody transfer and recovery | `src/features/lost-found/LostFound.tsx`, backend lost-found service/controller | Latest full backend suite passed against isolated PostgreSQL 16 + pgvector: 201 passed, 0 failed, 1 intentional live-provider opt-in skip. Hosted account walkthrough remains outstanding. |
| In-site notifications and account settings | `src/features/account/Account.tsx`, `DirectoryController.cs` | Backend integration suite passed; no request-update email is introduced. |
| Protected administration, roles and scopes | `src/features/admin/Admin.tsx`, `CampusDirectory.cs`, `DirectoryController.cs` | Backend integration suite passed. Ordinary registration does not grant staff roles. |
| Named statistics, open workload, resolution metrics, weekly trend and printable report | `StatisticsController.cs`, `src/features/admin/Admin.tsx` | Backend integration suite passed, including named buildings, workload metrics and map-position concurrency checks. Counts use authorized scopes and documented rules. |
| Interactive demo map and unplaced-building list | `CampusLocation.MapX/MapY`, migration `20261112110000_LocationMapPositions`, admin endpoint `PUT /api/v1/admin/locations/{id}/position` | Admin-only endpoint validates 0–100 coordinates and concurrency version. It is schematic, not geographic. The additive migration was applied to the connected Supabase project after confirming `Locations` had zero rows. Columns and EF history row were verified. |
| Staff-confirmed recurring faults | `StatisticsController.cs` | Derived from saved operational report fields; AI assessments are excluded. PostgreSQL integration coverage passed. |
| AI excluded from user-facing functionality | Maintenance, lost-found and admin screens | AI features are visibly deferred; historic AI suggestions are hidden and no fabricated scores/results are shown. |
| Rehearsal identities and prepared recovery inventory | `DemoPreparationService.cs`, `AdministrationController` | Admin-only endpoint is off by default, creates five independent `DEMO ONLY` identities, synthetic photos and records, and two 30-day item-specific recovery grants for the recovery requester. Initial credential response is shown once; later calls are read-only. PostgreSQL integration coverage passes. No hosted demo accounts have been created yet. |

## Verification completed

- Backend: latest `dotnet test backend/SmartCampus.slnx --configuration Release` with isolated local PostgreSQL 16 + pgvector — 201 passed, 0 failed, 1 intentional live-provider opt-in skip. The live-provider path was tested separately with the replacement key: six free synthetic API requests passed.
- Frontend: latest Vitest 80/80, TypeScript typecheck, ESLint and Vite production build passed.
- Browser: Chromium and WebKit Playwright E2E 28/28 passed, including keyboard focus, guest access boundaries, responsive layouts at 390/768/1440 and both languages. Firefox launch failed on this Windows host (`spawn UNKNOWN`) before tests could run.
- `dotnet ef migrations has-pending-model-changes` reported no pending model changes after aligning the `PositionVersion` default and concurrency token.
- `git diff --check` passed.
- After the hosted additive migration, `https://smartcampus.tryasp.net/health/ready` returned HTTP 200. This is a health check, not an authenticated user-flow test.

## Hosted database change and recovery evidence

The connected Supabase project is on the Free plan, whose dashboard reports no scheduled project backups. To preserve the user's free-services constraint, no plan upgrade was made. Before the additive migration, the target `smart_campus.Locations` table was confirmed empty, and a targeted schema snapshot plus EF SQL and rollback SQL were saved under `outputs/database-backup-2026-11-12/`. This is a targeted snapshot, not a full database backup. The migration added nullable `MapX`/`MapY`, non-null `PositionVersion` with default 1, and the matching EF history row atomically. The added columns and history row were verified after apply. No existing location rows were changed.

## Release gates still open

- Authenticated journeys on the hosted site using fictional test accounts. The protected setup control is off by default; after temporary enablement, confirm five separate roles, recovery/transfer journeys, private images, and then turn the host setting off.
- A real verification/recovery email sent only to the approved test mailbox `23012888@o6u.edu.eg`, followed by inspection of Brevo events and received DKIM/DMARC headers. Sender/domain readiness and actual inbox placement remain unverified.
- Domain authentication is managed by the university. DNS inspection found SPF without a Brevo include and DMARC at monitoring policy, but this alone does not establish the status of Brevo's account-specific DKIM/domain checks. No DNS changes were made. Brevo's exact authentication records must be obtained from its authenticated domain settings and supplied to the university DNS administrator. See [Brevo's domain authentication guide](https://help.brevo.com/hc/en-us/articles/12163873383186-Authenticate-your-domain-with-Brevo-Brevo-code-DKIM-DMARC).
- A full pixel-by-pixel comparison of all 18 design boards at 390/768/1440 and the email-client preview matrix have not been recorded.
- The current branch has an automatic MonsterASP deployment workflow on push. A push deploys the locally verified build, but does not close the real-email, hosted account-flow, or complete visual comparison gates.

## Security review

Changed API/data paths were checked for access control, input bounds, output encoding, concurrency handling and secret exposure. Map edits are admin-only, bounded and version checked; an integration test verifies role denial, stale-version conflict and out-of-range rejection. Email action links are restricted to absolute HTTP(S) URLs without credentials and HTML encoded. Provider keys are not added to templates or the browser bundle. No dependencies were added. The production database migration was additive and scoped to the empty `Locations` table. The Supabase Free plan does not provide a full scheduled backup; the targeted snapshot limitation is documented above.

The Supabase security advisor also reports legacy findings in the `public` schema, including callable `SECURITY DEFINER` functions and an enabled-RLS table without policies. Read-only checks found no `anon`/`authenticated` usage or table grants for the application's `smart_campus` schema. The legacy function grants and definitions were not changed because they are outside the ASP.NET application's schema and their ownership/consumers need a separate review. Treat those advisor findings as unresolved until that review confirms whether the functions are exposed through another service.

## Work performed — 12 November

- Added locally served Noto Sans Arabic and Nunito Sans WOFF2 files with SIL OFL notices; refined responsive navigation and account layouts.
- Replaced AI-dependent maintenance and lost-item features with clear deferred states, and removed historic AI suggestions from staff UI.
- Changed the production fallback release policy to accept any syntactically valid exact university-domain address, while retaining an explicitly configured `controlled` policy. Limited the Resend test-recipient allowlist to its shared `@resend.dev` sender so configured owner-only acceptance testing cannot block other students' transactional email when a verified sender is used.
- Added the default-off admin demo preparation flow, five independent fictional accounts, private synthetic found-item images, scoped demo directory/maintenance data, and a 30-day item-specific recovery grant. Repeat requests detect completion and disclose no credentials.
- Branded verification and recovery email HTML with site colors, logo, bilingual copy, text alternative, responsive layout, safe links and email-compatible font fallbacks.
- Added named statistics, a schematic map with admin-only position editing, an unplaced-location list, trend/workload metrics and print view.
- Fixed EF migration snapshot drift and a PostgreSQL translation failure in recurring-fault statistics discovered during integration testing.
- Applied and verified the additive location-map migration on the connected Supabase project after confirming the target table was empty; saved targeted recovery artifacts outside the source repository.
- Reviewed all 18 design boards and recorded remaining hosted account, real email, pixel comparison and Supabase legacy-schema review gates above.

## Current task verification — 12 November

- Release build: `dotnet build backend/SmartCampus.slnx --configuration Release` — passed, 0 warnings and 0 errors.
- PostgreSQL integration suite after the demo-preparation service cleanup: 178 passed, 0 failed, 0 skipped, using only the isolated local `_test` database.
- Frontend: Vitest 65/65, `npm run typecheck`, `npm run lint`, and `npm run build` passed.
- Browser: Chromium and WebKit 28/28 passed. Firefox 14/14 did not start because the local Firefox executable failed with Windows `spawn UNKNOWN`; no Firefox test body ran.
- `git diff --check` passed. No hosted data, application settings, DNS, or provider state were changed during this verification.

## Account eligibility and AI provider continuation — 13 November 2026 (unreleased)

- Updated both deployment templates to `Release__Mode=university`, matching the approved requirement for all exact `@o6u.edu.eg` accounts. The existing `ReleaseTests.University_mode_registers_any_campus_email_and_requires_confirmation_before_login` and domain-negative tests verify any university address, exact-domain rejection, and confirmation-before-login. Production MonsterASP variables were not viewed or altered.
- The replacement encrypted OpenRouter test key passed the opt-in application test for both maintenance modalities and passage/query embeddings: six fictional requests, all free models with zero price cap, no fallback, no retries. This proves API integration only, not accuracy or durable application workflow. Search remains fail-closed pending independent calibration and governance approval.
- Verification: backend 201 passed/1 skipped; frontend 80/80; typecheck, lint, production build and `git diff --check` passed. No browser, commit, push, merge, or deploy was used.

## Local implementation continuation — 13 November 2026 (unreleased)

This continuation changes the working tree only; it has not been committed, pushed, merged or deployed. The latest isolated PostgreSQL 16 + pgvector backend run passed 177/177. Frontend now passes 67/67 Vitest cases, typecheck, lint and build. It pins maintenance AI to the existing OpenRouter free model, reuses current-revision found-item vectors on resumed work, restores private text/image search UI, and removes operational AI evaluation/demo surfaces per the latest user scope. No real OpenRouter request was run and no retrieval calibration/accuracy is claimed. Brave manual inspection covered public home/auth/recovery/verification views, but not authenticated user/staff/admin journeys; the local API was unavailable and `/api/v1/auth/me` returned 500 through the Vite proxy. Therefore no full 55-requirement or end-to-end acceptance is claimed. See [BACKEND_PROGRESS.md](BACKEND_PROGRESS.md) for exact browser scope and remaining gates.

## Follow-up source fixes — 13 November 2026 (unreleased)

- The admin map editor is keyboard-operable: arrow keys move the point, Shift moves by five units, focus instructions are linked accessibly, and pointer placement has a zero-size layout fallback. A focused component test verifies keyboard placement and versioned save.
- EF Core's false-positive `First`/`FirstOrDefault`-without-order warning was removed for the already ordered/limited raw job query. Retryable maintenance-provider failures remain `pending`; only terminal failures show unavailable/error.
- Retrieval SQL now discovers and quotes the installed pgvector extension schema, so it works when the extension is installed outside `public`; regression coverage includes the isolated `extensions` schema.
- Repeated searches refresh an existing per-user result grant in place, even after expiry, instead of creating another temporary authorization row for the same result. The retrieval integration test verifies one grant remains after repeated searches and an expiry cycle.
- Hosted indexing reuses a persisted embedding only when its dimension matches the pinned 1024-dimensional model; a mismatched stored vector is regenerated and the dimension constraint is preserved.
- Removed orphaned AI Evaluation responsive/print styles from the operational stylesheet; general administration print styles remain. The frontend suite and production bundle passed after cleanup.
- Verification after these changes: PostgreSQL 16 + pgvector backend 177/177; frontend Vitest 68/68; typecheck, ESLint and production build passed. Brave was not used in this follow-up by user direction. No real provider request, complete 55-requirement acceptance, or live-site journey is claimed.

## Found-item list query follow-up — 13 November 2026 (unreleased)

`LostFoundService.List` now batches associated data rather than calling `Detail` per result. A related claimant was missing from the list even though item detail authorization permits it; the list now includes the claim-based access path. Integration coverage proves the same database read count for 1 and 100 rows (at most ten), and validates owner, receiving-department, claimant, outsider, and admin access plus contact/storage-key privacy. Complete Release backend suite: 178/178; the tightened focused read-count/privacy test also passed. No browser or production check was performed.

The admin accounts page also batches role, scope, and synthetic-account marker reads. Its integration test verifies bounded, page-size-independent reads and retained staff role/scope values. The complete Release backend suite after both list fixes passed 179/179.

## Owner-authorized uncalibrated search suggestions — 14 November 2026

Commit `082fe18530af59678420e591e8628fdbd052846a` allows the hosted Gemini retrieval route to return up to five similarity-ranked eligible candidates when no calibrated threshold exists. The response explicitly reports `calibrated: false`; the frontend labels these as uncalibrated suggestions and warns that unrelated items may appear. The real calibration state remains unapproved, and accuracy/No-Match targets are not claimed. Valid calibrated thresholds still filter candidates when present. Existing privacy approvals, free-tier setting, quota reservation, model/revision/dimension checks, indexing gate, visibility scopes and custodian review are unchanged. Set `Search__AllowUncalibratedCandidates=false` to reinstate the prior calibration gate.

The commit was pushed to the release branch and deployed successfully through [MonsterASP run 43536273569](https://github.com/MohamedTamer007/smart-campus/actions/runs/43536273569); [CI run 43536281786](https://github.com/MohamedTamer007/smart-campus/actions/runs/43536281786) passed. Isolated Release PostgreSQL tests: 226 passed, four external/evidence tests skipped; Vitest: 96 passed; typecheck, ESLint and production build passed. Public home and health returned 200, the protected found-items API returned 401 without a session, and the served JS bundle matched the local build. No live Gemini request or authenticated production search was run during this release. Retrieval calibration, production match quality, real university directory data, live email receipt/authenticated account journeys and full visual review remain open acceptance work.
