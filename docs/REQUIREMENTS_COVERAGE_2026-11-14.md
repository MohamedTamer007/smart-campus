# Current requirement coverage — 14 November 2026

This is an implementation and evidence index for all 55 source IDs, not a completion certificate. Local results prove only the named tested scenarios. Full visual, hosted, provider-quality and real-email acceptance remains open. Existing source approvals are preserved; proposed diagram integration decisions are not automatically additional requirements.

Source: supplied `Smart_Campus_Diagrams_5.0/TRACEABILITY_MATRIX.md`, referencing the merged requirements pages shown below; normative text is `docs/SMART_CAMPUS_REQUIREMENTS.md`. The explicit later Gemini request adds image-only search and supersedes the old description-required rule for that mode.

## Implementation map

All HTTP paths below use `/api/v1`. Tables are in `smart_campus`. Each requirement row points to this map by its implementation key.

| Key | Frontend | API | Data | Server rules |
|---|---|---|---|---|
| AUTH | `/login`, `/register`, `/verify`, `/recover`, `/reset`, `/settings` | `/auth/register,login,logout,me,resend,verify,recover,reset,profile,password` | Identity users/roles, Sessions, OutboundEmails, Jobs | Exact university domain, verified sessions, CSRF, anonymous recovery response |
| LOC | Maintenance forms; `/admin/directory` | `/directory/locations,departments,assignees/{id}`; `/admin/locations,departments` | Locations, Departments, Scopes | Parent hierarchy, active approved routing, admin mutations |
| MAINT | `/maintenance/new`, `/maintenance`, `/maintenance/:id`, `/staff` | `/maintenance`, `/{id}`, `/{id}/updates,cancel,ai-review` | Reports, History, Assessments, Jobs, Media, Notifications | Owner pre-handling edits; scoped staff; current version; AI separate from decision |
| FOUND | `/found/new`, `/found`, `/found/:id` | `/found`, `/found/{id}`, `/media/{id}` | Items, Media, Handovers, SearchGrants | Required image; owner/current custodian/scoped receiver; private media |
| CUST | `/found/:id`, `/requests` | `/found/{id}/handovers`, `/confirm,not-received,cancel` | Items, Handovers, Scopes, Notifications | Item lock; actual scoped receipt; pending source custody; no transfer while reserved |
| SEARCH | `/search`, `/found/:id` | `/found/search`, `/found/{id}`, `/media/{id}` | Embeddings, Items, SearchGrants, InferenceUsage | Verified actor; model/revision/dimension; reviewed thresholds; eligible candidates only |
| CLAIM | `/claims`, `/claims/:id` | `/found/{id}/claims`; `/claims/{id}/approve,reject,cancel` | Claims, Items, Notifications, Audit | Current custodian; private collection details; one active reservation; item lock |
| DEL | `/claims/:id` | `/claims/{id}/confirm` | Claims, Items, Notifications, Audit | Independent requester/custodian confirmations; atomic closure; idempotence |
| DASH | `/`, `/staff`, `/requests`, `/admin` | `/statistics`, authorized lists; `/admin/locations/{id}/position` | Reports, Items, Claims, Locations, Handovers, Scopes | Real scoped records, Cairo date boundaries, staff decisions, versioned map positions |
| NOTIF | `/notifications` | `/notifications`, `/{id}/read` | Notifications | Recipient-owned reads; related-record permission still enforced |
| UI | All operational routes; local CSS/fonts | APIs above; no browser provider key | Language preference/profile only | Route gates supplement server authorization; no user-facing AI evaluation/demo page |

Operational list endpoints use stable secondary UUID ordering for equal creation times. The guarantee applies to a dataset that is unchanged while paginating; concurrent inserts/deletes are not snapshot-isolated.

## Requirement-by-requirement evidence

| ID | Source PDF page | Source requirement summary | Implementation key | Evidence | Current acceptance boundary |
|---|---:|---|---|---|---|
| AUTH-01 | 6 | University email, site password and email-link verification. | AUTH | E01 | External: real mailbox receipt unverified |
| AUTH-02 | 6 | Normalize and validate the exact o6u.edu.eg domain. | AUTH | E02 | Local validation verified |
| AUTH-03 | 6 | Registration, login, verification, resend and recovery have dedicated flows. | AUTH | E01 | External: real links/mail delivery unverified |
| AUTH-04 | 6 | Accessible fields, show/hide password and clear validation errors. | AUTH | E03 | Partial: API token cases verified; password-manager purposes and new-password length guidance aligned with the server; full accessible error UI acceptance open |
| AUTH-05 | 6 | Backend-owned sessions, expiry and logout behavior. | AUTH | E04 | Local session scenarios verified |
| LOC-01 | 7 | Maintenance location requires faculty, building, floor and precise location. | LOC | E05 | Local synthetic hierarchy verified |
| LOC-02 | 7 | Use verified supplied location records; do not fabricate the directory. | LOC | E06 | External: verified institutional directory still required |
| LOC-03 | 7 | Building/floor options follow valid parent selections. | LOC | E06 | Local tree/parent scenarios verified; interactive visual acceptance open |
| LOC-04 | 7 | Unavailable location data has loading, failure and unavailable outcomes. | LOC | F01 | DOM loading/empty/error scenarios verified |
| MAINT-01 | 8 | Reporter supplies fault details and location, with optional image. | MAINT | E05,E07 | Local report workflow verified |
| MAINT-02 | 8 | Successful submission returns a report reference and tracking path. | MAINT | E07 | Local reference and persistence scenario verified |
| MAINT-03 | 8 | Main states are Submitted, Under Review, Being Repaired, Resolved and Rejected. | MAINT | E07,E08 | Local state transitions verified |
| MAINT-04 | 8 | Owner edit/cancel is permitted only before staff handling. | MAINT | E07,E08 | Local owner edit/cancel gates verified |
| MAINT-05 | 8 | Authorized staff review, assign, update and reject within granted scope. | MAINT | E07 | Local staff scope/assignment lifecycle verified |
| MAINT-06 | 8 | Fault type, priority and risk are staff-visible AI suggestions, not the saved staff decision. | MAINT | E09 | Local private suggestion/decision separation verified; hosted acceptance open |
| MAINT-07 | 8 | Submission, acknowledgement, review and repair are distinct events. | MAINT | E07 | Local event history verified |
| MAINT-08 | 8 | Unavailable AI analysis is shown honestly without invented labels. | MAINT | E10 | Local failure/restart cases verified; provider availability remains external |
| FOUND-01 | 8 | Found-item registration requires image, description, place, date and custody information. | FOUND | E05,E11 | Local upload/registration/privacy scenarios verified |
| FOUND-02 | 8 | Custody destination is a registered department with authorized accounts. | FOUND | E12 | Local registered destination and receipt workflow verified |
| FOUND-03 | 8 | Uploader and confirmed current custodian are distinct identities. | FOUND | E12 | Local uploader/custodian separation verified |
| FOUND-04 | 8 | Only the current confirmed custodian reviews recovery requests. | FOUND | E12 | Local current-custodian authorization verified |
| CUST-01 | 8 | Current custodian initiates a handover to a registered department. | CUST | E12 | Local request/receipt workflow verified |
| CUST-02 | 8 | Receiving department retains pending handovers until its staff confirms actual receipt. | CUST | E12,E13 | Local pending/received reservation gates verified |
| CUST-03 | 8 | Source custodian retains authority until actual receipt; authorized receiver confirms or reports non-receipt. | CUST | E12 | Local current authority/scoped receipt verified |
| CUST-04 | 8 | Pending handover can be cancelled before receipt; cancellation does not delete the item. | CUST | E14 | Local cancellation keeps original custody |
| SEARCH-01 | 9 | Verified user searches with a distinguishing description and optional prior image; the later Gemini amendment also permits image-only search. | SEARCH | E15,E25,F02 | Search returns ranked suggestions before calibration; real-model retrieval quality remains uncalibrated |
| SEARCH-02 | 9 | Matching considers requester description/image, found-item image and finder description. | SEARCH | E15,E25 | External: synthetic SQL ranking verified; independent Gemini ranking acceptance missing |
| SEARCH-03 | 9 | No public gallery; search returns only backend-authorized eligible candidates. | SEARCH | E11,E15 | Local access/grant rules verified; owner-authorized uncalibrated mode can show unrelated eligible candidates |
| SEARCH-04 | 9 | Similarity is not ownership proof; no invented confidence percentage or private finder phone disclosure. | SEARCH | E11,F02 | UI labels uncalibrated suggestions and warns they may be unrelated; visual acceptance open |
| SEARCH-05 | 9 | Loading, no-suitable-match, invalid-input and service-failure outcomes are distinct. | SEARCH | F02 | DOM service-error/empty/input distinctions verified |
| SEARCH-06 | 9 | Search/result access is authenticated and backend-authorized; delivered items are not eligible. | SEARCH | E11,E15 | Local server authorization and candidate eligibility verified |
| CLAIM-01 | 9 | A verified user selects a potential result and submits a request with optional message. | CLAIM | E16 | Local claim submission/review workflow verified |
| CLAIM-02 | 9 | Personal custodian decides and supplies phone only when approving; details are limited to accepted requester and authorized staff. | CLAIM | E17 | Local personal-custodian approval path verified |
| CLAIM-03 | 9 | Department custodian decides within department scope and supplies collection instructions/place. | CLAIM | E16 | Local scoped departmental approval verified |
| CLAIM-04 | 9 | One approval creates one active reservation. A second approval is prohibited; other existing claims remain pending. | CLAIM | E16,E13 | Local single reservation/cancellation verified |
| CLAIM-05 | 9 | Approval is separate from physical recovery and no financial transaction exists. | CLAIM | E16,E17 | Local approval and delivery remain distinct |
| CLAIM-06 | 9 | Backend atomically prevents concurrent approvals and duplicate active requester claims. | CLAIM | E16 | Local concurrent approval/duplicate prevention verified |
| DEL-01 | 10 | Current custodian and accepted requester make separate delivery/receipt confirmations. | DEL | E16,E17 | Local independent confirmation paths verified |
| DEL-02 | 10 | One confirmation leaves the request Approved and item Reserved. | DEL | E16,E17 | Local single-confirmation pending state verified |
| DEL-03 | 10 | Both confirmations close/deliver item and make other claims non-actionable. | DEL | E16 | Local closure and competing-claim outcome verified |
| DEL-04 | 10 | Backend performs final transition atomically and preserves both timestamps. | DEL | E16 | Local atomic closure/repeated confirmation verified |
| DASH-01 | 10 | Users see own reports, owned/held items, claims, relevant requests and notifications. | DASH | E18 | Local own-record counts verified; complete visual dashboard acceptance open |
| DASH-02 | 10 | Staff see assigned and department-scoped queues and handovers. | DASH | E07,E12 | Local staff scope verified; interactive queue acceptance open |
| DASH-03 | 10 | Admin sees systemwide activity, status distribution, recurrence and defined statistics. | DASH | E19 | Local named metrics/map update tests verified; hosted/visual acceptance open |
| DASH-04 | 10 | Statistics use real authorized records and documented counting rules. | DASH | E19,E20 | Local counting/date/scope rules verified |
| NOTIF-01 | 10 | Status/request notifications are in-site; email is reserved for verification and password recovery. | NOTIF | E18,E21 | Local recipient/event/email-boundary scenarios verified |
| NOTIF-02 | 10 | Inbox has unread/read state, authorized related-record links and mark-read actions. | NOTIF | E18,F03 | Local ownership/read and DOM update verified |
| UI-01 | 11 | Navy/blue/orange visual direction and white/light surfaces. | UI | F04 | Visual acceptance deferred; source/style and local font assets only |
| UI-02 | 11 | Arabic is default and English is fully supported. | UI | F04 | DOM Arabic default/language persistence verified; full translation visual audit open |
| UI-03 | 11 | Arabic RTL and English LTR brand layout. | UI | F04 | DOM direction verified; full RTL/LTR visual alignment open |
| UI-04 | 11 | Signed-in landing presents the two agreed services only. | UI | F04 | DOM two-service order verified; signed-in visual acceptance open |
| UI-05 | 11 | Guest access shows introduction and account entry, without service records. | UI | F04 | DOM guest service gate verified; visual acceptance open |
| UI-06 | 11 | Responsive fluid layout, usable scrolling and mobile-safe controls. | UI | F05 | Visual acceptance at 390/768/1440 deferred; code/layout changes not proof |
| UI-07 | 11 | Keyboard operation, visible focus, labels and adequate contrast are design baselines. | UI | F05 | Specific DOM keyboard/label tests passed; whole-site keyboard/contrast audit open |
| UI-08 | 11 | Translated labels/errors/states preserve language and RTL-safe IDs/contacts. | UI | F04,F06 | Specific language/label tests passed; full route/state visual audit open |

## Named evidence

Backend E references were checked against passed results in their named test runs and subsequently rerun in the latest full Release suite: `backend/SmartCampus.Tests/TestResults/full-after-gemini-harness-20261114.trx` (226 passed, 4 skipped, zero failed). The four skips are opt-in provider calls, bounded real Gemini retrieval, and saved-gallery replay; no external provider was called by that suite. E25 is owner-supplied output rather than a checked-in TRX. Frontend F references are from the full 94/94 Vitest run. DOM testing does not establish pixel layout, contrast, or actual Brave behavior.

| Ref | Exact test name / scope |
|---|---|
| E01 | `Registration_verification_recovery_and_replayed_tokens_follow_real_flows` |
| E02 | `University_email_is_normalized; Deceptive_email_is_rejected` |
| E03 | `Malformed_account_action_tokens_return_validation_errors; Expired_verification_and_reset_tokens_are_rejected` |
| E04 | `Missing_csrf_is_rejected_and_logout_revokes_session; Expired_database_session_is_rejected_even_with_a_cookie` |
| E05 | `Invalid_upload_and_inconsistent_location_are_rejected` |
| E06 | `Location_tree_groups_children_by_parent_and_does_not_cross_faculty_boundaries; Location_tree_returns_empty_collection_without_locations` |
| E07 | `Maintenance_scope_stale_updates_and_lifecycle_are_enforced` |
| E08 | `Prehandling_cancellation_and_rejection_preserve_truthful_states` |
| E09 | `Reviewed_analysis_stays_private_and_never_sets_staff_decision; Image_is_forwarded_only_after_explicit_approval_of_the_attached_image` |
| E10 | `Unreviewed_report_never_reaches_external_provider; Expired_leases_stop_at_three_attempts_and_preserve_completed_results` |
| E11 | `Private_inventory_media_and_uncalibrated_search_do_not_leak` |
| E12 | `Custody_changes_only_on_scoped_receipt_and_former_custodian_loses_authority` |
| E13 | `Reservation_blocks_transfer_and_cancellation_releases_existing_claims` |
| E14 | `Pending_handover_cancellation_keeps_item_and_original_custody` |
| E15 | `Hosted_description_only_indexing_and_search_work_without_worker_or_photo_disclosure` (also verifies explicit uncalibrated opt-in, response labeling, no request on the disabled path, and retained filtering when calibrated); `Image_only_and_combined_queries_rank_current_vectors_without_empty_text_requests; Pgvector_ranking_filters_candidates_and_persists_user_specific_result_grants` |
| E16 | `Concurrent_approvals_create_one_reservation_and_both_confirmations_close_it` |
| E17 | `Delivery_first_still_requires_independent_requester_receipt` |
| E18 | `Notifications_are_recipient_owned_and_dashboard_counts_use_real_records` |
| E19 | `Admin_statistics_return_named_buildings_workload_metrics_and_save_map_positions_with_version_checks` |
| E20 | `Workload_metrics_count_only_non_cancelled_reports_and_average_valid_resolution_times; Date_filter_includes_cairo_day_boundaries_and_excludes_the_following_midnight` |
| E21 | `Custody_notifications_exclude_inactive_or_unverified_department_accounts; Accepted_email_retains_provider_correlation_and_does_not_resend_after_commit` |
| E22 | `PaginationTests.Maintenance_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_scoped_to_the_reporter; Found_item_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_scoped_to_the_uploader; Claim_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_scoped_to_the_requester; Notification_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_recipient_owned; Paginated_operational_endpoints_require_an_authenticated_user; Maximum_page_numbers_do_not_overflow_any_operational_offset` |
| E23 | `GeminiReindexerTests.Terminal_reindex_failures_are_retried_only_after_an_explicit_generation_change` |
| E24 | `ProviderTests.OpenRouter_resizes_and_compresses_legacy_large_image_before_reserving_and_sending` |
| E25 | Owner-supplied run output for `GeminiLiveTests.Gemini_embedding_2_returns_768_dimensional_text_and_image_vectors_for_synthetic_inputs`: one real synthetic Arabic text and one generated image request passed; both asserted as 768 dimensions. Not present in the checked-in TRX, and not a retrieval-accuracy result. |
| F01 | `directory-options.test.tsx: explains why the handover department dropdown has no options; shows a directory load failure rather than an empty list` |
| F02 | `protected-forms.test.tsx: submits an image-only search without requiring or inventing a description; shows service failure separately from an empty search result` |
| F03 | `protected-forms.test.tsx: marks an owned notification read and refreshes its state` |
| F04 | `foundation.test.tsx: starts Arabic, hides the demo disclaimer and gates service actions behind login; keeps English when navigating and places maintenance before lost and found` |
| F05 | `admin-map-accessibility.test.tsx: lets an administrator place a building with arrow keys and saves the selected coordinates; no whole-site visual claim` |
| F06 | `maintenance-labels.test.ts: translates known fault types and severity levels; preserves unrecognized staff-entered labels and renders missing values clearly` |

## Remaining acceptance gates

- Independent Gemini retrieval calibration: current-revision text, image and combined queries; Arabic/English, confusable objects and no-match cases. Owner-authorized uncalibrated suggestions are enabled; the independent accuracy and No-Match acceptance criteria remain unproven.
- Real university directory/routing data. Synthetic fixtures do not fulfill LOC-02 for production.
- Real verification/recovery mail receipt and token action, provider correlation, DKIM/DMARC and delivery inspection. Local sinks/HTTP boundaries do not prove inbox placement.
- Interactive visual/accessibility and end-to-end acceptance at 390/768/1440 in Arabic/English is deferred under the user instruction to avoid device/browser testing now.
- Authenticated search against the production inventory and actual production Gemini calls were not exercised in this verification. Health and deployed client assets were checked separately.

Verdict: local implementation and scenario evidence exist across the 55 IDs, but all requirements have not achieved full end-to-end production acceptance. No 100% completion or deployment-readiness certification is claimed.

## Verification update — 14 November 2026

The base app at `8ee4ce9cc5e4bf581e80c83713722d6b2222ffca` had previously deployed successfully (MonsterASP workflow [43528071136](https://github.com/MohamedTamer007/smart-campus/actions/runs/43528071136)); the hosted retrieval migration still awaited calibration then. Commits `e19c901` and `87fc080` contain browser/test corrections. This history does not supersede the later release record below.

## Uncalibrated suggestion release — 14 November 2026

At the owner's request, commit `082fe18530af59678420e591e8628fdbd052846a` enables the owner-authorized similarity suggestion mode before retrieval calibration. When the active query mode lacks an approved threshold, the backend may return up to five similarity-ranked, eligible candidates and identifies the response as `calibrated: false`; it does not report a confidence or claim ownership. The client labels these as suggestions and warns that unrelated items may appear. A valid mode-specific calibrated threshold continues to take precedence. The separate privacy, provider, quota, revision, indexing, and access gates remain enforced. See [SEARCH_CANDIDATES_MODE.md](SEARCH_CANDIDATES_MODE.md) for the behavior and rollback setting.

MonsterASP deploy run [43536273569](https://github.com/MohamedTamer007/smart-campus/actions/runs/43536273569) and CI run [43536281786](https://github.com/MohamedTamer007/smart-campus/actions/runs/43536281786) both passed for this commit. Local Release integration tests against isolated PostgreSQL passed 226 with four explicit external/evidence tests skipped; Vitest passed 96, and typecheck, ESLint and production build passed. Public checks returned HTTP 200 for `/` and `/health`, 401 for `/api/v1/found`, and confirmed the downloaded live JavaScript bundle matched the local build byte-for-byte. No real Gemini API request or authenticated search against production data was made. Retrieval accuracy and the independent No-Match acceptance target remain unverified; overall completion is not claimed.

## Password-field follow-up — 14 November 2026

Source inspection found that the shared password field advertised `new-password` even on login. The login panel now explicitly uses `current-password`; registration, reset and new-password inputs use `new-password` with the server's 12–128 character limits and bilingual guidance. Current-password fields remain free of new-password length restrictions. Each show/hide control names and references its own input. Typecheck, ESLint and the 96-test frontend suite passed; these automated checks do not prove password-manager behavior or visual/accessibility acceptance in a real browser.
