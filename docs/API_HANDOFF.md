# API contract

Base `/api/v1`. React uses relative same-origin requests. Legacy service actor arguments are presentation only; the server derives identity and current grants. Development uses a Vite proxy. Cross-origin credential access is disabled.

## Transport

GET `/auth/csrf` issues the antiforgery cookie and returns `{token}`. Send `X-CSRF-TOKEN` on every non-GET API request, including login. Refresh after authentication changes. Session cookies are HttpOnly, SameSite Strict and secure in production, with server-backed revocation, 30-minute idle and eight-hour absolute limits. No browser bearer token is persisted.

Responses use camelCase, UUIDs, UTC timestamps and ISO dates. Lists use one-based `page`, `pageSize` default 50/max 100, returning `{items}`. Creation ordering is newest first; accounts sort by email. Location/department lookups are arrays. Principal response schemas are declared in `ResponseContracts.cs` and OpenAPI.

Mutations require the latest integer version. Grant changes use the account concurrency stamp. Allowed-action flags are hints, rechecked server-side. Reload conflicts before retrying.

Errors: 400 invalid input/CSRF/token, 401 missing/expired session or credentials, 403 unverified/forbidden, 404 missing record, 409 stale state/duplicate/reservation conflict, 429 rate limit, 503 dependency/calibration/index unavailable, 500 unexpected failure. Application ProblemDetails include `code`; binding errors include field errors. Framework authentication/rate-limit responses may have empty bodies. Never interpret 503 as no matches.

## Endpoint map

| Methods and paths | Inputs / access |
|---|---|
| POST `/auth/register`, `/auth/login` | Register name/email/password/confirmation; login email/password. Registration cannot grant roles. |
| POST `/auth/verify`, `/auth/resend` | userId/token or email. Verification links last 24 hours. |
| POST `/auth/recover`, `/auth/reset` | email or userId/token/password/confirmation. Generic recovery response; reset links last one hour. Production email is queued. |
| GET `/auth/me`; POST `/auth/logout`, `/auth/password`; PUT `/auth/profile` | Verified session. currentPassword/newPassword or name/language. Password changes revoke sessions. |
| GET `/directory/locations`, `/directory/departments`, `/directory/assignees/{departmentId}` | Verified users; assignees require maintenance scope/admin. Locations return nested faculty/building/floor options. |
| GET/POST `/maintenance`; GET/PUT `/maintenance/{id}` | Owner/scoped staff/admin. Multipart description/faculty/building/floor/details, optional photo; edits include version. |
| POST `/maintenance/{id}/cancel`, `/updates` | version; updates also status/optional assignee/note/optional decision `{type,priority,risk}`. Owner cancellation before handling; updates scoped staff/admin. |
| GET/POST `/found`; GET `/found/{id}` | Registration multipart required photo/description/place/date/storage, optional registered destination UUID and office. Lists are owned/held/scoped, never a public inventory. |
| POST `/found/search` | Multipart description (up to 2,000 characters), photo, or both; at least one is required. Photo is JPEG/PNG/WebP up to 5 MiB. Returns qualifying itemIds/rankingVersion/calibration; user-specific grants last 15 minutes. Each query mode requires its separately reviewed current-revision threshold. |
| POST `/found/{id}/handovers`, `/handovers/confirm`, `/handovers/not-received`, `/handovers/cancel` | Item version; initiation also department UUID/office. Current custodian initiates/cancels; intended department confirms/non-receipt. |
| POST `/found/{id}/claims`; GET `/claims`, `/claims/{id}` | Optional message. Creation requires a valid search grant, available item and non-custodian requester. Detail returns `{claim,item}`. |
| POST `/claims/{id}/approve`, `/reject`, `/cancel` | Claim version. Personal approval requires phone; departmental approval place/instructions. Rejection/cancellation requires reason. Current custodian only, no self-approval. |
| POST `/claims/{id}/confirm` | Claim version. Actor identity determines receipt or delivery; clients cannot supply another actor's timestamp. |
| GET `/media/{id}` | Reauthorizes related record and returns sanitized JPEG. No public bucket URLs. |
| GET `/notifications`; POST `/notifications/{id}/read` | Recipient only. Related links require separate authorization. |
| GET `/admin/accounts`; PUT `/admin/accounts/{id}/grants` | Admin. Exact roles/departmentIds arrays and concurrency stamp version. Self-modification prohibited; grants revoke target sessions. |
| POST `/admin/departments`, `/admin/locations` | Admin. Department code/ar/en/canReceive/maintenance; location parentId/kind/ar/en/maintenanceDepartmentId. Use verified data. |
| GET `/statistics?from=&to=&departmentId=` | Server applies ownership/scopes. Dates are inclusive Africa/Cairo dates. |
| GET `/admin/operations` | Administrator only. Aggregate job counts, application AI budgets, API memory, deployment identity and loopback worker/calibration status. No credentials, private payloads or user directory. |

## State rules and limits

Maintenance statuses: submitted/review/repair/resolved/rejected; cancellation uses cancelledAt, mapped to the existing React cancellation label. Handling begins at review; repair needs a scoped assignee. No reopening. Content versions invalidate only changed AI inputs. Assessments are pending/completed/unavailable and excluded from ordinary-user content; staff decisions remain separate.

Item state: available/reserved/delivered. Handover: pending/not-received/confirmed/cancelled. Non-receipt remains open; department selection does not transfer custody. Reservation guards run both at initiation and receipt.

Claims: pending/approved/rejected/cancelled/closed. Approval holds one reservation; existing pending claims survive cancellation. Both independent confirmations close recovery; competing claims receive ItemRecovered. React displays the recovered closed claim as delivered. Repeated confirmation creates no additional event. Cancellation is custodian-only before either confirmation; no automatic expiry exists.

Search scores are ranks, never ownership probabilities. Up to five qualifying matches require reviewed calibration. Pending/failed indexing means unavailable, not no-match. Phone/instructions are restricted to accepted requester and authorized operational actors.

One JPEG/PNG/WebP image up to 5 MiB, maximum 16 million decoded pixels, sanitized to JPEG. Maintenance image optional; found image required. Descriptions/messages max 4000; locations/offices max 1000. Passwords 12–128 characters; exact normalized o6u.edu.eg domain.

Statistics use each record's creation date and current authorized scope. Cancellation counts separately. Fault types use saved staff decisions; recurrence groups fault type/floor with counts above one. Under review is not all open work. Notifications are in-site; email is verification/reset only.

Deployment templates use `Release:Mode=university`, which permits any syntactically valid exact `@o6u.edu.eg` account after required email verification. Registration does not grant staff/admin privileges. `Release:Mode=controlled` remains available only for an intentionally restricted test deployment; unlisted registration/login/private access then returns HTTP 403 with `release_restricted`, and recovery/resend stay generic. Development defaults to open access on loopback, while registration still validates the exact university domain.

AI attempts consume atomic UTC day/minute application counters before provider calls; failed attempts count. OpenRouter defaults are 50/day and 20/minute. NVIDIA defaults to a zero daily budget until permitted quota is explicitly configured. These are application budgets, not a live account-credit report. Quota deferral carries a reset time; applicable API errors include `Retry-After`.

`POST /maintenance/{id}/ai-review` takes `{version, confirmedNonSensitive, includeImage}`. Current scoped maintenance staff or administrators must explicitly review the exact report content version before external analysis. Images require separate approval, an eligible configured provider, and the globally enabled image permission. The endpoint increments the report version and requeues its assessment; it does not change maintenance status or staff decisions. A running job or repeated approval returns 409. Owner edits create a new unreviewed assessment. Late results cannot populate the edited version. Staff DTOs include `assessment.inputReviewed`, `canReviewAiInput` and `canReviewAiImage`; ordinary-user assessments remain null.

Cairo date bounds use the first actual instant of the calendar date, including skipped midnight at DST. Pending claims are filtered by claim creation date and current custodian department; the item's registration date does not exclude a new claim. Dashboard grouping runs in PostgreSQL.

Session and CSRF GETs remain globally rate limited but do not spend the authentication mutation budget. Default IP limits remain 120 overall requests/minute and 15 auth mutations/minute; CI fixtures use higher explicit limits for rapid synthetic journeys. Session restoration runs once per application mount and cannot overwrite a newer sign-in or revive an expired session.
