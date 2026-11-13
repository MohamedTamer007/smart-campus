# Smart Campus - Requirements and Team Handoff

Version: 1.0 | Date: 2 November 2026

Status: agreed product requirements and approved visual direction; not an implementation or test-results report.

Audience: the frontend developer, the .NET/backend developer, and the graduation-project team.

## 1. Purpose and ownership

Smart Campus is an October 6 University project with two services: maintenance reporting and lost-and-found management. The frontend uses React. The backend uses .NET and owns authentication, permissions, storage, workflow rules and AI-provider integration.

The frontend developer implements all user-facing interfaces, responsive layouts, Arabic/English presentation, forms, feedback and API integration. The backend developer implements the API and connects NVIDIA and OpenRouter for AI. The browser must not call those providers directly or contain their secret keys.

This specification records the team's decisions. Requirements marked **Agreed** are product decisions confirmed in the discussion. **Baseline** items are ordinary engineering requirements proposed to make those decisions work safely. **Integration decisions** must be settled with the backend developer; they are not claims about existing endpoints or code.

No application code, API contract or model accuracy is certified by this document. The 18 visual boards are design references. Sample names, dates, locations, counts and object photos are illustrative. If small generated image text conflicts with this specification, this specification is authoritative.

## 2. Scope and exclusions

**Agreed:** provide maintenance reports, private lost-item search, found-item registration, custody transfer, recovery requests, bilateral delivery confirmation, request tracking, in-site notifications and simple role-specific dashboards.

There are no academic services, campus news, events, payments, refunds, live chat or public gallery of found items. There are no request-update emails. Email is used for account verification and password recovery. The current delivery adds an admin-only schematic demo map with manually saved building positions, clearly distinguished from a geographic campus map. Custom model training, provider quotas and AI infrastructure are backend concerns, not new frontend services.

The source proposal mentions spatial maps and broader decision support. The approved 12 November 2026 release plan expands the dashboard to include an explicitly schematic demo map, selectable date/departments, named building counts, unplaced locations, weekly trend, open-work and resolution metrics, and printable reporting. It does not claim geographic accuracy or use AI outputs as staff decisions.

## 3. Users and permissions

| Actor | Access |
| --- | --- |
| Guest | Read the introductory home page and access sign-in/registration; no service records, search results or submission actions. |
| Unverified account | Complete verification or recovery; no maintenance submission, lost-item search, found-item submission or claim actions. |
| Verified university user | Submit and track own maintenance reports; register found items; search by description; request recovery; manage claims on items currently in their custody. |
| Authorized staff | Work on assigned reports and items within their granted department/scope; confirm departmental custody and review claims on those items. |
| Admin | Assign account roles and departmental scopes; manage the agreed administrative views and see systemwide activity/statistics. |

**Agreed:** Admin grants staff privileges. Registration never includes a role selector and possession of a university email does not grant staff status. Security, secretariat and maintenance are examples of responsibilities, not unrestricted administrator accounts.

Custodian is an item-specific responsibility. The person who originally uploaded an item and its current custodian may be different. After confirmed transfer, the previous custodian loses the right to approve recovery for that item.

**Baseline:** the API checks authorization on every read and write. Hiding a button is not authorization. A staff member cannot access another department merely by changing a record ID. The actor cannot confirm a delivery on behalf of the other party. A single staff account may also use ordinary-user services.

## 4. Registration, login and account recovery

**AUTH-01 - Agreed:** register with a university email, a password specific to this site, and email-link verification. Do not use Google/Microsoft sign-in.

**AUTH-02 - Agreed:** the normalized email domain must equal `o6u.edu.eg`. Compare case-insensitively and trim surrounding whitespace. Validate complete email syntax as well. Do not accept lookalike domains or suffixes such as `o6u.edu.eg.example.com`; do not accept subdomains unless the team explicitly changes this rule. Frontend feedback and server validation are both required.

**AUTH-03 - Agreed:** account creation, login, verification, resending a verification message, password recovery and setting a new password have dedicated interfaces. Service actions require a verified authenticated account.

**AUTH-04 - Baseline:** show/hide password controls, labelled fields and clear field errors are required. Reject mismatched password confirmation. Expired/invalid verification and recovery links have a readable state and an action to request another link. Do not expose whether an email exists through recovery responses.

**AUTH-05 - Baseline:** session expiry returns the user to login with an explanation. Preserve non-sensitive form text where feasible; do not preserve passwords. Logout removes authenticated access. Session transport, expiry and password rules are API-owned integration decisions, not fixed numbers in a mockup.

Account settings show the verified email, allow display-name and language changes, provide password change and logout. Email changes and account deletion are outside this version's agreed scope. A phone number is not required at registration; it is requested when a student custodian approves a claim.

## 5. Locations and form conventions

**LOC-01 - Agreed:** a maintenance report requires faculty/facility, building, floor and precise location details. All four have a visible required marker. The report description is also required. The image is optional.

**LOC-02 - Agreed:** do not fabricate a complete campus building/floor directory. The team will supply the actual building/floor information. Official university pages can seed verified faculty names but do not prove a complete room-level inventory.

**LOC-03 - Baseline:** building options should follow the selected faculty/facility, and floors should follow the selected building where data supports this. Use actual records supplied by the backend. Ground floor must be a valid value when it exists; do not guess floor counts. Clearing/changing a parent selection clears any incompatible child selection.

**LOC-04 - Baseline:** location lists need loading, failed-load and unavailable-data states. Do not silently submit a false default location. A missing production location must be resolved in the shared location data rather than replaced with a fictional building.

Required fields retain entered values after a recoverable failure. Upload controls show a preview, allow removal/replacement, and explain unsupported files or limits. Accepted image types, size limits, image count and text limits come from the agreed API configuration. Numbers accidentally shown in a generated mockup are not an approved upload/password/text policy.

## 6. Maintenance workflow

**MAINT-01 - Agreed:** the user describes the fault, selects its location, optionally adds an image and submits. Examples include a leaking tap, broken door, failed air conditioner or lighting fault. The user does not fill in AI fault classification, priority or risk.

**MAINT-02 - Agreed:** successful submission provides a report reference and a way to open report tracking. A success state must follow a confirmed API success, not a button click alone. Failed submission keeps recoverable input for retry.

**MAINT-03 - Agreed:** visible main statuses are Submitted, Under review, Being repaired, Resolved and Rejected. Rejection requires a written reason. The backend defines allowed transitions and returns the current authoritative state.

**MAINT-04 - Agreed:** the user can edit or cancel only before handling begins. The backend must enforce this even if the screen is stale. Once handling begins, do not leave an active edit/cancel control that implies permission still exists. No post-review edit-request workflow is added here.

**MAINT-05 - Agreed:** authorized staff can review, assign a report to authorized maintenance personnel, update its status and add relevant updates within their scope. Report details show the user's description, location, attachments when present, status and update history.

**MAINT-06 - Agreed:** AI fault type, suggested priority and suggested risk are visible to authorized staff/Admin only. Keep priority and risk separate. Label the output as a suggestion and distinguish it from the staff's saved operational decision. Staff can review/change the decision according to permissions. The ordinary reporter must not see internal AI outputs.

**MAINT-07 - Baseline:** submission and a staff acknowledgement are different events. Do not label a newly stored report as physically inspected or repaired. Assignment, review and rejection updates must identify their relevant time and actor/scope.

**MAINT-08 - Baseline:** if analysis is unavailable, show an honest staff-side unavailable/pending state; do not invent a priority. The implementation must agree with the backend how a stored report is returned while analysis is delayed. No response-time or repair-time promise is made.

Cancellation must be representable without pretending the fault was resolved or rejected. The exact API state or cancellation flag is an integration decision. Changing terminal states or reopening reports is not an approved automatic workflow.

## 7. Found-item registration

**FOUND-01 - Agreed:** require an item image, description, place found, date found and current storage/custody information. The image is required here, unlike maintenance.

**FOUND-02 - Agreed:** the custody choices are With me, Security, Secretariat and Other approved department. When a department is selected, require the registered receiving department and enough office/location detail to identify the intended handover.

**FOUND-03 - Agreed:** Other means a department registered in the system with authorized accounts; it is not an arbitrary text name that gains access. Free text can describe an office but cannot grant permission.

**FOUND-04 - Agreed:** only the current confirmed custodian can review recovery requests. Selecting Security or Secretariat does not prove physical delivery and does not immediately transfer custody.

The place-found field is required. Unlike the maintenance-location rule, the discussion did not establish that every found item must be inside a building. The registration design shows an indoor example; do not reject a valid outdoor place merely because that example includes a floor. The exact place-found control can reuse available locations plus a required precise description.

**Baseline:** validate dates and required fields; show errors close to their fields; do not publish contact details in an item description. The frontend must not encourage people to expose personal ID-card details in photos. Production image-access controls remain the backend's responsibility.

## 8. Custody handover

**CUST-01 - Agreed:** the current custodian starts a handover request to a registered department. It is Pending receipt until an authorized member of that receiving department confirms actual receipt.

**CUST-02 - Agreed:** while receipt is pending, the original custodian retains recovery-approval responsibility. A receiving staff member can confirm receipt or report that the item has not been received. After confirmation, custody and recovery permissions move to the department.

**CUST-03 - Agreed:** a pending handover can be cancelled before confirmation. Do not confuse cancelling a handover with deleting the item.

**CUST-04 - Baseline:** scope the incoming queue to the intended department. A confirmation is atomic and repeat-safe. Two staff members acting on the same handover must not create two custodians.

**Integration decision:** define how an in-progress approved recovery is handled if someone attempts a new custody transfer. Recommended baseline: block the transfer while that reservation is active; do not silently change an accepted claimant's recipient or contact instructions.

## 9. Private lost-item search

**SEARCH-01 - Agreed:** a verified user searches using a required distinguishing description. A previous image of the lost item is optional. Search is limited to records uploaded to this site, not Google or external inventories.

**Approved user amendment — Gemini migration request:** support description-only, image-only, and description-plus-image queries. At least one input is required. The original description-required rule above is superseded for image-only queries; privacy, eligible inventory and independently calibrated matching remain mandatory. Implemented locally on 14 November 2026; this is not production activation or retrieval-quality evidence.

**SEARCH-02 - Agreed:** matching considers the owner's description and optional image, the found-item image, and the description submitted by the finder. The backend owns retrieval, analysis and matching thresholds.

**SEARCH-03 - Agreed:** there is no public inventory gallery. Before a search, do not expose all found-item thumbnails. After the backend returns a qualifying potential match, the user can see the matching item's photo, description, place/date found and custody details, then open a recovery request.

**SEARCH-04 - Agreed:** a similarity result is a potential match, not an ownership certificate or automatic recovery approval. Do not show invented confidence percentages. A qualified match does not make the finder's private phone public.

**SEARCH-05 - Baseline:** distinguish loading, no suitable matches, invalid input and service failure. A provider failure must not be labelled 'no matching item exists'. Allow the user to revise the description and retry. Do not promise a fixed search duration.

**SEARCH-06 - Baseline:** enforce authenticated access to result details and return only records the requester may view under the search policy. Do not depend on unguessable IDs or a hidden frontend list. Delivered/closed items cannot be offered as available recovery candidates.

Saved automatic searches, automatic future-match alerts, lost-item image training and a public browsing page are not added by this specification.

## 10. Recovery requests and temporary reservation

**CLAIM-01 - Agreed:** the user selects a potential result and sends a recovery request. An extra message is optional. The current confirmed custodian receives the request.

**CLAIM-02 - Agreed:** when the item is with a student, that student reviews and approves/rejects requests. Student approval requires a contact phone number at the approval step. Show it only to the accepted requester and authorized operational actors, not to other claimants or visitors.

**CLAIM-03 - Agreed:** when a registered department has confirmed custody, its authorized staff review requests. Staff approval requires collection instructions and an identifiable collection place; a personal phone number is not mandatory for that path.

**CLAIM-04 - Agreed:** one approval temporarily reserves the item for one requester. Do not accept a second request while that reservation is active. Other existing requests remain pending until final delivery or cancellation of the approval.

**CLAIM-05 - Agreed:** approval and physical collection are separate. There is no payment, refund or purchase in this service. Notifications and buttons must use 'recovery/collection' meaning, not financial return language.

**CLAIM-06 - Baseline:** the backend must atomically prevent two approvals, including simultaneous clicks by two staff members. Disable in-flight submit actions and safely handle repeated requests. A person should not claim an item they currently hold; prevent duplicate active requests by the same person for the same item.

**Integration decisions:** confirm whether new claims can be submitted while an item is reserved; only the prohibition on a second approval is fixed. Define who can cancel a recovery approval and at which pre-delivery stages. No automatic timeout/release period was agreed. Keep the existing pending claims when a reservation is legitimately released.

The site provides the approved phone/instructions for arranging collection. It does not include a live chat system or guarantee ownership merely because a description matches.

## 11. Bilateral delivery and closure

**DEL-01 - Agreed:** the current custodian confirms 'I delivered the item' and the accepted requester independently confirms 'I received the item'. Each action needs an explicit confirmation step and can be performed only by the correct actor.

**DEL-02 - Agreed:** a single confirmation leaves the recovery awaiting the other party. Approval alone and the first delivery confirmation must not close the item.

**DEL-03 - Agreed:** after both confirmations, the recovery and item are closed/delivered. No new recovery requests can be submitted for that item. Remaining pending claims must stop being actionable and receive the appropriate closed outcome.

**DEL-04 - Baseline:** the backend performs the final transition atomically and returns both confirmation flags and closure state. Repeated confirmation must not create duplicate events or notifications. Preserve timestamps for both confirmations.

If the parties disagree or one does not confirm, show the truthful pending state. A dispute-resolution service, automatic forced closure or a specific student-affairs support route was not agreed and must not appear as an existing service.

## 12. Dashboards, tracking and notifications

**DASH-01 - Agreed:** the normal user sees their maintenance reports, recovery requests, found items they registered/hold, relevant incoming requests and in-site notifications. Simple counts are enough; do not add arbitrary charts to fill space.

**DASH-02 - Agreed:** staff see their authorized departmental/assigned queue, applicable custody handovers, recovery requests and modest statistics for their scope. Maintenance staff may review AI suggestions only where permitted.

**DASH-03 - Agreed:** Admin sees systemwide totals, maintenance status distribution, recurring fault types, issue distribution by recorded location, found-item availability and pending recovery work. The expanded admin view also has a schematic demo map, open-report count, resolved share, average and oldest-open durations, weekly trend and print view. Map positions are administrator-maintained display coordinates and do not establish geographic locations.

**DASH-04 - Baseline:** statistics are derived from real authorized records and respect applied date/scope filters. Define each count to avoid treating 'Under review' and all open work as the same metric. All numbers in design images are sample data, not measured project results.

**NOTIF-01 - Agreed:** request/status notifications appear inside the site only. Email messages are restricted to verification and password recovery. In-site notifications cover relevant maintenance updates, incoming/accepted/rejected recovery requests, custody receipt and delivery progress.

**NOTIF-02 - Baseline:** show unread/read state and link to an authorized related record. Reading a notification must not grant permission to its target. The user can mark notifications read. Loading, empty and fetch-failure states are required. The polling or push mechanism is an integration decision; no browser-push permission or background email feature is assumed.

## 13. Visual system, language and accessibility

**UI-01 - Agreed:** use the approved navy/blue/orange direction with white/light surfaces, restrained borders and the current placeholder logo. The product name is **Smart Campus**, never Smart Canvas. Do not add an Arabic replacement brand name or an O6U subtitle in this version.

**UI-02 - Agreed:** Arabic is the initial default. English is available through a language switch. A separate English screenshot set is not required. Both language versions must function, not just the language switch graphic.

**UI-03 - Agreed:** Arabic uses RTL: the brand group is at the header's right edge; the symbol is to the right of Smart Campus. English uses LTR: the brand group is at the left edge; the symbol is to the left of the name. Keep the brand text readable in LTR inside either layout and do not mirror the logo artwork itself.

**UI-04 - Agreed:** the signed-in home greeting and short service subtitle sit over the university image on both desktop and mobile. It has two service cards only: Maintenance and Lost and found, including the found-item registration action. Maintain service order on mobile: Maintenance, then Lost and found.

**UI-05 - Agreed:** guests see introductory content and login/register calls to action, without a personal greeting or service data. Replace generated campus imagery with the supplied original university photograph during implementation. The generated building/banner and object photos are visual references, not approved production assets or factual university records.

**UI-06 - Baseline:** use fluid responsive layouts, readable text and normal vertical scrolling. Do not shrink an entire long form to fit one phone screen. Fixed mobile navigation must not cover form controls, errors or final actions. Tables should adapt to readable cards or another usable small-screen presentation.

**UI-07 - Baseline:** target keyboard operation, visible focus, labelled form fields, meaningful headings, alt text and adequate text/control contrast. Required markers must have an accessible text equivalent; status must not rely on color alone. Respect reduced-motion preferences. These are acceptance targets, not claims that mockups passed a browser accessibility audit.

**UI-08 - Baseline:** translate labels, validation, status messages and empty/error states. Isolate email addresses, phone numbers and IDs so RTL does not reorder them incorrectly. Preserve the chosen language while navigating. Date storage/display rules must be agreed with the backend; use consistent campus-facing dates and times.

## 14. Visual review package index

The design PDF has a cover and an index, followed by these 18 boards. Board 01 therefore appears on PDF page 3. Several boards intentionally show alternative states or role perspectives together; they are not a single live page displaying contradictory states at once.

| Board | Design group | PDF page |
| --- | --- | --- |
| 01 | Signed-in home | 3 |
| 02 | Maintenance report form | 4 |
| 03 | Submission confirmation and tracking | 5 |
| 04 | User dashboard and My requests | 6 |
| 05 | Private lost-item search | 7 |
| 06 | Search results and potential-match details | 8 |
| 07 | Found-item registration | 9 |
| 08 | Departmental custody handover | 10 |
| 09 | Recovery request and custodian review | 11 |
| 10 | Collection instructions and bilateral delivery | 12 |
| 11 | Login and registration | 13 |
| 12 | Verification and password recovery | 14 |
| 13 | In-site notifications and account settings | 15 |
| 14 | Maintenance staff dashboard and report review | 16 |
| 15 | Departmental found-item dashboard | 17 |
| 16 | Admin overview and simple statistics | 18 |
| 17 | Accounts, roles and departments | 19 |
| 18 | Arabic guest home | 20 |

Edit forms reuse the relevant submission layout. Empty, loading, error, expired-link, rejection-reason and confirmation states belong to these groups; they are not unapproved new services. Navigation highlights should follow the actual current section even if a generated reference highlights Home.

## 15. Frontend/backend integration handoff

The following are **proposed operation and data needs**, not existing URL paths, C# symbols, database schemas or an already implemented contract. Agree on endpoint names and exact DTOs with the backend developer before integration.

| Operation family | Data/results the frontend needs |
| --- | --- |
| Identity | Register, verify/resend, login/logout, recovery/reset, current account, granted roles/scopes and settings. |
| Locations | Valid faculties/facilities, related buildings, related floors and departments authorized to receive items. |
| Maintenance | Create, own-list, authorized detail, pre-handling edit/cancel, staff queue, assignment, allowed status updates, rejection reason and staff-only AI assessment. |
| Found items | Create, own/custodian lists, authorized detail, current confirmed custodian and availability state. |
| Handover | Request/cancel transfer, incoming departmental queue, confirm or decline receipt and updated custody. |
| Search | Owner description and optional image; qualifying matching-item summaries/details; explicit unavailable/no-match distinction. |
| Recovery | Create optional-message request, sent/incoming lists, approve/reject, conditional phone/instructions, reservation state and allowed actions. |
| Delivery | Each party's independent confirmation, waiting/closed states and closure result. |
| Notifications | Authorized list, unread/read information, related record and mark-read operation. |
| Administration | Scoped/systemwide statistics, existing account role/scope assignment and registered departments. |

Recommended response data includes stable record references, timestamps, state, server-calculated allowed actions and a concurrency/version token. Maintenance data separates the AI suggestion from the saved staff decision. Custody data separates uploader, current custodian, proposed receiving department and transfer state. Claim data distinguishes applicant, decision, reservation, private collection information, each confirmation and final closure.

For lists, agree on filtering, sorting and pagination. For validation, return machine-readable error codes and field-level errors that the frontend can translate. For stale actions, return the latest record state and a clear conflict result. Do not accept actor/role/custodian identity supplied by the browser as authorization.

The backend developer must confirm: API base address and CORS; session/cookie-or-token handling; verification/recovery links; file-upload transport, validation and limits; private image access; location IDs; state/transition definitions; search-result access rules; approval cancellation/reservation behavior; concurrency conflicts; notification delivery; and statistics definitions. These dependencies do not change the two-service scope.

## 16. Ordinary engineering safeguards

These are **Baseline** implementation requirements supporting the agreed product, not additional public features.

- Enforce frontend validation for usability and repeat it on the server for correctness.
- Prevent unauthorized cross-account, cross-department and unverified access on the API.
- Store and handle passwords and sessions on the backend with suitable security controls; do not invent authentication in React.
- Never commit credentials or expose provider, database-admin or service-role keys in the frontend bundle. Use server-controlled private media access where needed.
- Validate uploads on the server, not by filename alone. File constraints must be shared with the frontend before implementation.
- Prevent double submission and conflicting approvals/transfers/confirmations. Re-read authoritative state after a conflict.
- Render user text safely; do not treat descriptions or AI output as executable HTML or instructions.
- Return truthful loading/empty/error/success states; never replace a failed service call with fabricated successful records.
- Keep recovery phone details limited to the accepted request's collection context and authorized operational access.
- Do not load the large research datasets into the frontend bundle or expose training/evaluation data as public found items.
- Use only illustrative seed records in demos, clearly labelled. Production statistics and workflow states must come from the real API.
- Keep the site usable on mobile, by keyboard and with assistive technologies; test the rendered result rather than relying on mockups alone.

Rate limits, session duration, upload sizes, password policy and retention periods must be configured with the backend. This document does not invent numerical policies or promise that a free tier has unlimited capacity.

## 17. Acceptance checklist

The following checks are planned acceptance criteria, **not tests already executed against a website**.

1. A guest can read the home introduction but cannot submit, search or retrieve private item details.
2. Invalid/non-university domains are rejected in the form and by the API; letter case does not break a legitimate university domain.
3. A newly registered but unverified account cannot use services; verification and invalid/expired-link paths work.
4. Password confirmation mismatch, wrong login credentials and generic recovery feedback have readable states.
5. A valid maintenance report requires description and all four agreed location fields, but succeeds without an image.
6. Failed submission preserves recoverable input; one click/retry cannot create uncontrolled duplicates.
7. A reporter can track only authorized reports and cannot see staff AI priority/risk/type fields.
8. Editing/cancelling works before handling and is rejected if handling started while the screen was stale.
9. Staff assignment/status updates obey scope; a rejection cannot be saved without a reason.
10. Found-item registration requires image, description, place/date and custody information.
11. Merely selecting a department does not transfer custody; confirmation by that receiving department does.
12. The old custodian loses recovery-approval rights after confirmed transfer; unrelated staff cannot confirm receipt.
13. Search requires a description, supports an optional image and searches only site records.
14. No public inventory appears before search; qualifying results show item details without exposing an unapproved student phone.
15. A search-service failure is distinguishable from a genuine no-match result.
16. A recovery message is optional. The current custodian, not necessarily the uploader, reviews the request.
17. Student approval requires a phone; departmental approval requires collection instructions/place.
18. Two simultaneous approvals cannot create two accepted reservations; other existing requests stay pending.
19. Each party can confirm only their own delivery/receipt; one confirmation cannot close the item.
20. Both confirmations close the item and prevent future claims; repeated confirmation is harmless.
21. In-site notifications link only to authorized records and can be marked read. No request-update emails are sent.
22. User, departmental and Admin dashboards use the correct record scope and real count definitions.
23. Arabic is the initial language; English works throughout, including errors and statuses; header brand position follows direction.
24. Mobile forms scroll naturally and fixed navigation does not hide actions. Keyboard focus and readable contrast are verified.
25. Empty lists, unavailable location data, missing optional photos, upload errors, expired sessions and stale record actions are handled explicitly.
26. Provider/storage secrets do not appear in committed files, browser requests to third-party AI endpoints or generated frontend assets.

During implementation, run the actual repository test, typecheck, lint and build commands that exist. Do not invent command names or report a browser/security/performance check as passed unless it was executed. This handoff adds documentation and visual concepts only.

## 18. Remaining implementation inputs and change control

The main business rules above are agreed. Before coding their integrations, the team still needs the verified building/floor inventory, real receiving departments and staff scopes, the finalized API contract and session configuration, upload constraints, search threshold/access policy, approval-cancellation details and operational definitions for dashboard metrics. These are deliberate coordination points, not imaginary completed features.

Use the supplied original university photo for the built site; keep the current logo as a placeholder until a logo is selected. Correct minor generated wording during implementation, including the Home navigation highlight on inner pages and any overly broad role warning. For Admin statistics, make the card name match its counting rule: for example, 12 under-review reports is not automatically 12 total open reports.

The team can send changes using the design PDF page number, element and requested correction. Record approved requirement changes in this file before implementation. No separate English design-image set is requested; English behavior is still required.

## 19. Sources

- The supplied project Word document: *Smart Campus: An Intelligent Decision Support and AI Platform for Maintenance and Lost-and-Found Management*; objective and expected outputs.
- The four supplied registration-form photographs; project objective and expected outputs.
- The project owner's subsequent decisions in this planning conversation, including the narrower dashboard scope, private search, custody confirmation and bilingual layout.
- Official university reference pages consulted for possible location data: https://o6u.edu.eg/?id=70 and https://o6u.edu.eg/dpages.aspx?FactId=15&id=10791 . These are references, not a verified complete building/floor directory.

## 20. Version record

| Version | Date | Summary |
| --- | --- | --- |
| 1.0 | 2 November 2026 | Consolidated the agreed frontend scope, backend handoff, 18-board visual index, normal engineering baselines and implementation coordination points. |
| 1.1 | 12 November 2026 | Expanded Admin analytics per approved release plan; added schematic, manually positioned demo map and print view. |
