# Reference board review

Source: the 18 approved planning boards on pages 71–88 of `استمارة تسجيل مشروع التخرج_merge.pdf`. Each source page was rendered and visually scanned on 12 November 2026. Current evidence includes the deployed source/build and local Chromium/WebKit screenshots at 390, 768 and 1440 CSS pixels. This is a feature/layout review, not a pixel-difference approval of every protected screen.

| PDF page | Screen | Current implementation | Review |
|---:|---|---|---|
| 71 | Signed-in home | `src/app/Home.tsx`, `.hero`, `.services` | Campus photo, welcome state and two service cards are present. Local screenshot check was performed for the guest state; signed-in rendering still needs an authenticated visual capture. |
| 72 | Maintenance report form | `src/features/maintenance/Maintenance.tsx` | Form uses live department/location directory, validation and optional private photo. Backend integration tests cover submission. |
| 73 | Submission tracking | `Maintenance.tsx`, `MaintenanceService.cs` | Status, history and owner/staff permissions are implemented and covered by backend tests. |
| 74 | My requests dashboard | `Admin.tsx` (`Requests`) | Shows the signed-in user's reports, items and claims. Backend privacy tests pass. |
| 75 | Private lost-item search | `LostFound.tsx` (`SearchItems`), `RetrievalService.cs` | Intentionally deferred with AI. The page explains the delay; no fake matches or scores are shown. This board is not functionally matched until the embedding model passes the approved benchmark. |
| 76 | Search result/match details | `LostFound.tsx` and media authorization | AI matching remains deferred, so no result-detail screen is presented as if a match existed. Item-level access and private media rules are tested independently. |
| 77 | Found-item registration | `LostFound.tsx` (`RegisterFound`), `LostFoundService.cs` | Description, date, location, custody and photo form are implemented. Backend upload tests use synthetic images. |
| 78 | Custody handover and receipt | `LostFound.tsx`, `LostFoundService.cs` | Version-checked handover, accept/not-received/cancel and department scope are implemented and integration tested. |
| 79 | Recovery request and review | `LostFound.tsx`, `LostFoundService.cs` | Request, approval and rejection flows are implemented and integration tested. |
| 80 | Collection and bilateral delivery | `LostFoundService.cs` | Reservation, staff delivery and independent requester receipt are implemented; both confirmations are required to close recovery. |
| 81 | Login and registration | `src/features/auth/Auth.tsx`, `AuthController.cs` | Arabic/English forms are implemented; production default accepts the exact university domain and requires verification. |
| 82 | Verify/reset email | `CampusEmailContent.cs`, `AuthController.cs` | Branded responsive HTML and text alternative are implemented. Live Brevo delivery and inbox authentication are still unverified. |
| 83 | Notifications and account settings | `src/features/account/Account.tsx` | In-app notifications and settings are implemented; only the current user's notifications are returned. |
| 84 | Maintenance staff dashboard | `Maintenance.tsx`, `StatisticsController.cs` | Scoped queues and staff decisions are implemented. AI recommendations are kept out of the decision controls. |
| 85 | Department found-item dashboard | `LostFound.tsx` | Department-scoped inventory and handovers are implemented; granted recovery items are limited to explicit active item grants. |
| 86 | Admin overview and statistics | `src/features/admin/Admin.tsx`, `StatisticsController.cs` | Named buildings, workload metrics, map, unplaced locations and print view are implemented. |
| 87 | Accounts, permissions and departments | `Admin.tsx`, `CampusDirectory.tsx`, `DirectoryController.cs` | Role/scope editing and directory management are admin-only and version checked. |
| 88 | Guest home | `src/app/Home.tsx`, `src/styles/app.css` | Updated to show guest state, sign-in and registration, then the campus image and service cards, matching the reference order. Added a browser assertion for the order and links. |

## Explicit release differences

- Boards 75–76 depend on AI matching. The current release explicitly defers this work and does not imitate a successful search.
- A full screenshot comparison of every authenticated page and the 18 reference boards at 390/768/1440 remains open. Current multi-browser screenshots cover guest layouts, keyboard behavior, direction and responsive width; protected account-based review requires the demo setup to be enabled and run on the hosted site.
- Mail-client previews, real test delivery to `23012888@o6u.edu.eg`, and Brevo/DKIM/DMARC evidence remain open.
