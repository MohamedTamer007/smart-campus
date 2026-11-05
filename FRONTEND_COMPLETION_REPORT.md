# Frontend handoff

- Scope: Arabic-first frontend screens and validation for maintenance, lost and found, account and administration workflows.
- Current integration: no backend, sample accounts, simulated records, or test controls are shipped. Backend-dependent actions report that the service is unavailable; private routes require sign-in.
- Security: client-side route gates do not provide authorization. The backend must enforce access, ownership, privacy and state transitions.
- The campus photograph and O6U emblem are included. Representative public screenshots in `docs/screenshots/` cover home, sign-in, registration and recovery at 390, 768 and 1440 CSS pixels. Protected screens require a real authenticated session and are not represented by fabricated screenshots.

## Backend integration

`docs/API_HANDOFF.md` lists the provisional frontend service boundaries and decisions that must be agreed with the backend team. It does not define external endpoints. Authentication, data persistence, notifications, uploads, location and department directories, permission management, matching and email delivery remain to be connected. The full behavior and screen requirements are in `docs/SMART_CAMPUS_REQUIREMENTS.md`.

## Verification

The project defines `dev`, `test`, `typecheck`, `lint`, `build`, `test:e2e` and `preview` scripts in `package.json`. Automated frontend checks cover local validation, unavailable operations, absence of demo controls and sample data, guest route gating, keyboard navigation, language direction, and responsive public layouts. Test-only mocked service responses exercise retained protected form controls without shipping a simulated service.

The previous tests that exercised the removed in-memory implementation no longer verify real backend behavior. Authentication, server-side authorization, ownership, privacy, stale updates, concurrency, custody and delivery must be tested against the actual backend after integration; they are not claimed as verified by this frontend.

To regenerate the public screenshots, build the application, start `npm run preview -- --port 4175 --strictPort`, then run `node scripts/capture-screenshots.mjs` from the repository root. The browser test configuration starts its own production preview on port 4174.

Cloudflare Pages is configured to build `main` with `npm ci && npm run build`, publishing `dist` at <https://smart-campus-o6u.pages.dev>.
