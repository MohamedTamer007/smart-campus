# 12 November release alignment

This matrix maps the approved non-AI release request to code and recorded acceptance evidence. The 18 design boards in the supplied project document were reviewed as visual references. They contain illustrative data; this implementation does not present illustrative values as live operational records. AI analysis and matching remain deferred.

| Requirement | Implementation | Acceptance evidence / current state |
|---|---|---|
| Arabic/English visual direction, original campus image, university mark | `src/app/App.tsx`, `src/app/Home.tsx`, `src/styles/app.css`, `src/assets/campus-photo.webp`, `public/o6u-mark.png` | Current local run: Chromium and WebKit passed 28/28 at 390/768/1440 for the covered public and guest flows, in both directions. Firefox could not launch on this Windows host (`spawn UNKNOWN`); compare the visual boards individually before claiming pixel-level parity. |
| Locally served Noto Sans Arabic and Nunito Sans | `src/assets/fonts/`, `src/styles/app.css` | Production bundle contains local WOFF2 assets and SIL OFL notices; no Google Fonts import. |
| All exact `@o6u.edu.eg` accounts can register and receive transactional messages | `ReleasePolicy.cs`, `AuthController.cs`, `EmailSender.cs`, `ResendEmailTransport.cs` | Production fallback is `university`; explicit `controlled` host configuration still intentionally applies an allowlist. Confirmation is mandatory before sign-in. A configured test recipient now limits only Resend's shared `@resend.dev` sender, not a verified sender. Integration and transport tests pass; real provider send/receipt remains outstanding. |
| Account verification and recovery email matching site brand | `CampusEmailContent.cs`, `BrevoEmailTransport.cs` | Automated tests cover branded HTML, safe action links, text fallback and recipient routing. A real message to the approved test mailbox `23012888@o6u.edu.eg`, Brevo event, authentication headers and inbox placement remain unverified. |
| Maintenance submission, review and tracking | `src/features/maintenance/Maintenance.tsx`, backend maintenance service/controller | Current full backend suite passed against isolated PostgreSQL 16 + pgvector: 178 passed, 0 failed, 0 skipped. Hosted account walkthrough remains outstanding. |
| Found-item registration, custody transfer and recovery | `src/features/lost-found/LostFound.tsx`, backend lost-found service/controller | Current full backend suite passed against isolated PostgreSQL 16 + pgvector: 178 passed, 0 failed, 0 skipped. Hosted account walkthrough remains outstanding. |
| In-site notifications and account settings | `src/features/account/Account.tsx`, `DirectoryController.cs` | Backend integration suite passed; no request-update email is introduced. |
| Protected administration, roles and scopes | `src/features/admin/Admin.tsx`, `CampusDirectory.cs`, `DirectoryController.cs` | Backend integration suite passed. Ordinary registration does not grant staff roles. |
| Named statistics, open workload, resolution metrics, weekly trend and printable report | `StatisticsController.cs`, `src/features/admin/Admin.tsx` | Backend integration suite passed, including named buildings, workload metrics and map-position concurrency checks. Counts use authorized scopes and documented rules. |
| Interactive demo map and unplaced-building list | `CampusLocation.MapX/MapY`, migration `20261112110000_LocationMapPositions`, admin endpoint `PUT /api/v1/admin/locations/{id}/position` | Admin-only endpoint validates 0–100 coordinates and concurrency version. It is schematic, not geographic. The additive migration was applied to the connected Supabase project after confirming `Locations` had zero rows. Columns and EF history row were verified. |
| Staff-confirmed recurring faults | `StatisticsController.cs` | Derived from saved operational report fields; AI assessments are excluded. PostgreSQL integration coverage passed. |
| AI excluded from user-facing functionality | Maintenance, lost-found and admin screens | AI features are visibly deferred; historic AI suggestions are hidden and no fabricated scores/results are shown. |
| Rehearsal identities and prepared recovery inventory | `DemoPreparationService.cs`, `AdministrationController` | Admin-only endpoint is off by default, creates five independent `DEMO ONLY` identities, synthetic photos and records, and two 30-day item-specific recovery grants for the recovery requester. Initial credential response is shown once; later calls are read-only. PostgreSQL integration coverage passes. No hosted demo accounts have been created yet. |

## Verification completed

- Backend: `dotnet test backend/SmartCampus.slnx --configuration Release` with isolated local PostgreSQL 16 + pgvector — 178 passed, 0 failed, 0 skipped.
- Frontend: Vitest 65/65, TypeScript typecheck, ESLint and Vite production build passed.
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
