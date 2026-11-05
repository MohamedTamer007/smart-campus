# Smart Campus frontend

Arabic-first React frontend for campus maintenance and lost-and-found workflows. The project contains screens, navigation and client-side validation; it has no connected backend, sample accounts or simulated records. Backend-dependent actions currently report that the service is unavailable. Private screens remain behind the client-side sign-in gate until a real session is connected.

The gate is a presentation aid, not authorization. The backend must enforce access, data privacy and workflow rules. The only persisted browser preference is the selected language. Unsubmitted drafts can remain in page memory while navigating.

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

The Playwright configuration uses `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` if provided, otherwise `/usr/bin/chromium` if present, otherwise Playwright's installed browser. Install the Playwright Chromium browser if the machine has neither an executable override nor the system browser. Browser tests build the project and start a production preview. `npm run preview` also serves an existing build locally.

For the public-screen screenshots, start `npm run preview -- --port 4175 --strictPort`, then run `node scripts/capture-screenshots.mjs` from the repository root.

## Cloudflare Pages deployment

Production site: <https://smart-campus-o6u.pages.dev>. The Cloudflare Pages project is connected to this GitHub repository and deploys from `main`. Build command: `npm ci && npm run build`; output directory: `dist`; root directory: repository root. No build environment variables are required.

## Backend handoff

There is no live API adapter yet. The frontend's unavailable service boundaries and integration questions are described in `docs/API_HANDOFF.md`; the behavioral requirements are in `docs/SMART_CAMPUS_REQUIREMENTS.md`. Actual endpoint paths, DTOs, session transport and error contracts must be agreed with the backend team. Until then, signing in, registering, changing account data, submitting reports and using lost-and-found operations cannot succeed.

`npm run build` produces `dist/`. The campus photograph and O6U emblem are used in the homepage and branding. Actual locations, departments and operational policies require backend integration.
