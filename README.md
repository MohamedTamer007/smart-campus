# Smart Campus frontend

Arabic-first React demo of maintenance and lost-and-found workflows for users, staff and administrators. The demo is deliberately isolated from real authentication, storage, email and AI services. Its records and accounts exist only in browser memory and reset when the page reloads. Language preference is stored in browser local storage.

To sign in, use `ahmed@o6u.edu.eg` or another demo actor's email and the demo password `demo`. New registrations are simulated in memory; their sign-in password is also `demo`. Registration passwords are not retained. These simulated accounts and role checks do not provide production authorization.

## Run locally

The installed Vite version requires Node.js `^20.19.0 || >=22.12.0` (see the lockfile and installed package metadata).

```sh
npm ci
npm run dev
```

The development server prints its local URL. Do not put API, GitHub, Cloudflare, Supabase or AI-provider credentials in frontend files or browser-exposed variables.

## Checks

```sh
npm test
npm run typecheck
npm run lint
npm run build
npm run test:e2e
```

The Playwright configuration uses `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` if provided, otherwise `/usr/bin/chromium` if present, otherwise Playwright's installed browser. Install the Playwright Chromium browser if the machine has neither an executable override nor the system browser. Browser tests start an isolated test-mode server with test-only scenario controls and a production preview; the controls are excluded from the production build. `npm run preview` serves the built output locally.

## Cloudflare Pages deployment

Production site: <https://smart-campus-o6u.pages.dev>. The Cloudflare Pages project is connected to this GitHub repository and deploys from `main`. Build command: `npm ci && npm run build`; output directory: `dist`; root directory: repository root. No build environment variables are required. Direct refresh of `/maintenance/new` was verified after deployment.

## Backend handoff

There is no live API adapter yet. Proposed operation families, integration decisions and missing inputs are in `docs/API_HANDOFF.md`; the authoritative behavioral reference is `docs/SMART_CAMPUS_REQUIREMENTS.md`. Simulated roles do not provide authorization in a real system.

`npm run build` produces `dist/`. The current deployment is a static frontend demo; it does not provide real persistence, authentication or backend authorization.

The supplied campus photograph and O6U emblem are now used in the homepage hero and site branding. Production locations and departments, backend contract, session/upload policies and some workflow decisions remain outstanding. Demo locations, departments and statistics are illustrative. See `FRONTEND_COMPLETION_REPORT.md` for tested coverage and limitations.
