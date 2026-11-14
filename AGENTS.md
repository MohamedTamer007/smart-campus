# Smart Campus development

Preserve the approved two-service scope and existing Arabic/English design. Never expose service keys in React. Authorization belongs to the API; client account arguments are presentation only.

Backend: .NET 10, EF Core/Npgsql, ASP.NET Core Identity, PostgreSQL. Run `dotnet build backend/SmartCampus.slnx` and `dotnet test backend/SmartCampus.slnx`. Integration tests require `SMART_CAMPUS_TEST_DB` pointing to an isolated database; never point tests at the hosted project. Frontend: `npm ci`, `npm test`, `npm run typecheck`, `npm run lint`, `npm run build`, `npm run test:e2e`.

EF migrations own the private smart_campus schema. Do not drop, reset, or migrate existing public tables. Do not apply hosted migrations at startup. Preserve AI trial records. Production deployment and shared-branch publication require explicit authorization.

Every mutation checks authenticated identity, current grants, resource scope and version. Recovery approval and custody operations lock the same item row. Only both independent confirmations close recovery. Keep AI suggestions separate from operational decisions. Search stays disabled without reviewed calibration.

Record executed commands, outcomes, blockers and acceptance evidence in docs/BACKEND_PROGRESS.md. No fabricated test results or production data. Secrets use .NET user-secrets or environment injection, never committed configuration. Do not commit build outputs, local mail or model weights.
