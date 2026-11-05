# Frontend integration boundary

Status: proposals for the teammate, not existing endpoints. This app is explicitly demo-only until a live contract is supplied. It makes no authentication, email, AI-provider, Supabase or backend requests.

The frontend's domain operations use asynchronous typed methods. The demo boundary can return unavailable, expired, conflict, forbidden or invalid failures. Failed requests never fall back to successful fixture records. Demo actors and role controls are simulation only.

The backend must own authorization on every operation, session transport/expiry, verification/recovery delivery, private image access, upload validation, assignment scope, reservation concurrency, custody transitions and final delivery closure.

Before a live adapter is written, agree on OpenAPI/DTOs, API base/CORS, cookie-or-token handling, machine-readable field errors, authoritative allowed actions, version tokens, cancellation flags, notification mechanism, pagination, location IDs, upload types/sizes/counts and text/password policies. No numerical policies or endpoint paths are assumed here.

Open business decisions remain: claims while reserved; who may cancel approval; transfer while approval is active. The demo will block ambiguous transitions rather than invent expiry or forced closure.

Demo records and location choices are illustrative, held in memory and reset by browser reload. Language preference alone is stored locally. Never put service credentials into browser-exposed environment variables.

Demo account grants are resolved from current in-memory records for each maintenance, lost-and-found and administration operation. Role changes that would make an approved recovery impossible are blocked in the demo. These are interaction examples, not backend enforcement or an agreed endpoint contract.

A saved registration, verified state, demo role/scope, display name, report, image, claim, notification or handover is lost on browser reload. Non-sensitive unsent maintenance and found-item text is retained only in page memory while navigating during that same page session. A simulated account's registration password is not retained; known and simulated registered accounts use the disclosed demo-only sign-in password.
