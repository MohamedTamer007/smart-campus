# Frontend integration boundary

The project is frontend-only. No authentication, directory, maintenance, lost-and-found, email, upload or AI service is connected. No endpoint paths or transport contracts are assumed. Backend-dependent operations currently reject with an unavailable state rather than fabricate success or records.

## Current frontend boundaries

- `src/features/auth/Auth.tsx` contains the sign-in, registration, verification, recovery and reset screens. Its backend-dependent actions call `backendUnavailable` from `src/services/unavailable.ts` after applicable local validation. Verification and reset links do not establish a session.
- `src/services/frontend.ts`, `src/services/maintenance.ts`, `src/services/lostFound.ts` and `src/services/admin.ts` contain provisional typed frontend operations for account settings, notifications, locations, reports, items, claims and account administration. They reject as unavailable until replaced by an agreed adapter.
- `src/app/context.tsx` starts as a guest; `src/app/access.tsx` gates private screens in the UI. These client-side gates are not security controls. A real session adapter must populate and clear account state, and the backend must authorize each operation.
- `src/services/validation.ts` validates the currently specified university-email domain locally. The backend team should confirm email and password policies. Image previews are local to the browser; upload handling is not connected.

Before implementing an adapter, agree on OpenAPI/DTOs, API base and CORS, session transport and expiry, verification/recovery delivery, machine-readable errors, authoritative allowed actions, version tokens, private media access, upload limits, directory/location identifiers, notification delivery and pagination. The backend must enforce ownership, role/scope access, reservation concurrency, custody transitions and delivery closure.

Open business decisions remain: claims while reserved, who may cancel approval, and transfers while approval is active. Do not infer endpoint paths or production policy from the frontend's provisional types.

The selected language is stored in browser local storage. Unsaved form drafts may remain in page memory during navigation; no records or credentials are persisted by a backend in this project. Never put service credentials in browser-exposed variables.

The former in-memory service tests exercised a removed simulation and cannot verify a real backend. Backend integration tests must cover authentication, authorization, ownership, privacy, stale writes, custody, concurrency and delivery once the backend exists.
