# API documentation

The maintained endpoint/DTO guide is [API_HANDOFF.md](API_HANDOFF.md). The generated schema is [openapi.json](openapi.json). Both describe `/api/v1`; production does not expose the development OpenAPI endpoint.

Authentication uses a server-backed cookie. Mutating API calls require the current `X-CSRF-TOKEN`; obtain it from `/api/v1/auth/csrf` and refresh it after authentication changes. The browser must share the API origin. Authorization, current grants, scope and allowed actions are evaluated by the backend.

HTTPS email transport is server configuration and does not change authentication request/response DTOs. The Resend integration has isolated transport tests; this is not proof that a real mailbox received a message. Hosted email setup remains blocked as described in [DECISIONS.md](DECISIONS.md).
