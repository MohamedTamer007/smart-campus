# Smart Campus

React/TypeScript frontend and ASP.NET Core 10 backend for maintenance and private lost-and-found workflows. Includes PostgreSQL/pgvector, Identity cookie sessions, private media, department scopes, transactional reservations, bilateral delivery, notifications, administration and AI adapters.

Source is pushed on `codex/backend-integration` with [draft PR #1](https://github.com/MohamedTamer007/smart-campus/pull/1). No public backend deployment or university release has occurred. Real pinned-model CPU inference passed in CI; calibrated retrieval, hosted integration and full release acceptance remain incomplete. See the acceptance report before describing this as ready.

- [Windows setup](docs/SETUP_WINDOWS.md)
- [API handoff](docs/API_HANDOFF.md) and [OpenAPI](docs/openapi.json)
- [Architecture](docs/ARCHITECTURE.md)
- [Deployment](docs/DEPLOYMENT.md)
- [Free hosting and Outlook setup](docs/HOSTING_SETUP.md)
- [Progress](docs/BACKEND_PROGRESS.md) and [acceptance evidence](docs/ACCEPTANCE.md)
- [Product requirements](docs/SMART_CAMPUS_REQUIREMENTS.md)

Configure the database and API using the setup guide. Run `npm ci`, then `dotnet run --project backend/SmartCampus.Api --urls http://127.0.0.1:5080` with Development environment enabled. In another terminal run `npm run dev -- --host 127.0.0.1`. Vite proxies API requests; no backend secret belongs in frontend variables. `scripts/publish.ps1` prepares the single-origin application without deployment.

Checks: `dotnet build backend/SmartCampus.slnx`, `dotnet test backend/SmartCampus.slnx`, `npm test`, `npm run typecheck`, `npm run lint`, `npm run build`, `npm run test:e2e`, and `python -m unittest discover -s worker -v`.

PostgreSQL tests require `SMART_CAMPUS_TEST_DB` pointing to an isolated loopback database ending `_test`; they reset only its application schema. Without it they are skipped. Worker boundary tests use a fake model and do not establish inference quality. General search requires reviewed calibration and completed indexing.

The existing Git-connected Cloudflare Pages frontend deployment remains unchanged and does not deploy this backend.
