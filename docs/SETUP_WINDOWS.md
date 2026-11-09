# Windows development

Use .NET SDK 10.0.100 or newer 10.0 feature band, Node 24, Python 3.13, and PostgreSQL 16 or 17 with pgvector. `global.json` selects .NET 10. Docker is optional; the development run used PostgreSQL 16 in the existing WSL Ubuntu installation.

## Database and configuration

Create independent development and test databases, for example `smart_campus_dev` and `smart_campus_test`. In an administrator psql session:

```sql
CREATE ROLE smart_campus LOGIN;
\password smart_campus
CREATE DATABASE smart_campus_dev OWNER smart_campus;
CREATE DATABASE smart_campus_test OWNER smart_campus;
```

Use an interactive password prompt rather than committing a password. The test suite refuses non-loopback connections and names that do not end `_test`. Never use the hosted database for tests.

As a database administrator install pgvector in each database:

```sql
CREATE SCHEMA IF NOT EXISTS extensions;
CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA extensions;
GRANT USAGE ON SCHEMA extensions TO smart_campus;
```

The example assumes a development role named `smart_campus` already exists and owns those local databases. Set its password locally. The API connection needs `Search Path=smart_campus,extensions,public`; migrations detect pgvector's installed schema without relocating it. Supabase's HTTPS API URL is not a PostgreSQL connection string.

Copy `backend/SmartCampus.Api/appsettings.Example.json` to `appsettings.Local.json` beside it, set the connection, and leave development mail/media enabled. This file is Git-ignored and excluded from publish. Alternatively use `dotnet user-secrets --project backend/SmartCampus.Api set ConnectionStrings:Campus ...` or environment injection. Do not put secrets in frontend variables.

When using WSL PostgreSQL, keep the distribution running while connecting from Windows. If localhost is refused, check `pg_lsclusters`, start the cluster, and keep a WSL terminal open. Do not expose PostgreSQL to the LAN to fix loopback connectivity.

## Run

From the repository root:

```powershell
npm ci
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/SmartCampus.Api -- --migrate
dotnet run --project backend/SmartCampus.Api --urls http://127.0.0.1:5080
```

In a second terminal run `npm run dev -- --host 127.0.0.1`. Vite proxies `/api` to port 5080, so browser requests remain same-origin. Open the Vite URL printed by the terminal. Local mail is written below the API's `local-data/mail`; open the verification/reset link from the appropriate `.eml` file. That proves local delivery only, not SMTP delivery.

For a single ASP.NET origin, run `scripts/publish.ps1` and launch `dotnet work/publish/SmartCampus.Api.dll` with configuration supplied by environment variables. Set `PublicOrigin` to the actual browser origin. A published directory does not include development secrets.

## Accounts and directory

Register a real university account and confirm its email. Set `Bootstrap:Email` securely, then run `dotnet run --project backend/SmartCampus.Api -- --bootstrap-admin`. This promotes an already verified account; it never creates a default administrator password.

For isolated local demonstrations only, `--seed-demo` creates labelled synthetic accounts/locations and writes random credentials to the API's ignored `local-data/demo-accounts.json`. It refuses production mode, remote hosts and databases not ending `_dev`. Run once; an existing receipt prevents accidental repeat seeding. These records are not verified campus locations.

Administrators create verified departments/locations through the API. Staff assignments require both role and department scope. The account screen can add roles/scopes and remove individual grants; choosing ordinary-user access removes privileged roles.

## AI worker

```powershell
python -m venv work/worker-venv
work/worker-venv/Scripts/python.exe -m pip install -r worker/requirements.txt
$env:HF_HOME = "$PWD/work/model-cache"
$env:SMART_CAMPUS_WORKER_KEY = 'SET_A_RANDOM_VALUE_OF_AT_LEAST_32_CHARACTERS'
work/worker-venv/Scripts/python.exe worker/worker.py --port 8123
```

Configure the identical `Search:WorkerKey` on the API. Keep the worker on loopback. Allow several GB of free disk for weights, dependencies and cache. The pinned model weights alone are approximately 1.5 GB. Worker startup loads the selected revision; its response carries the actual vector dimension. No fixed embedding dimension is imposed by the migration.

Set provider keys securely, supply reviewed `AI:FaultTypes`, `AI:Priorities` and `AI:Risks`, then enable non-sensitive inference only for inputs permitted by the provider terms. Image forwarding requires its separate explicit flag. NVIDIA is text-only. No automatic provider fallback or paid routing exists.

General search remains unavailable until `Search:CalibrationApproval` and `Search:MinimumScore` record a reviewed calibration. Jobs must also complete indexing. Failed workers/indexes are not treated as empty results.

## Checks

```powershell
dotnet build backend/SmartCampus.slnx
$env:SMART_CAMPUS_TEST_DB = 'YOUR_ISOLATED_LOOPBACK_TEST_CONNECTION'
dotnet test backend/SmartCampus.slnx --logger trx
python -m unittest discover -s worker -v
npm test
npm run typecheck
npm run lint
npm run build
npm run test:e2e
```

Without the test connection, PostgreSQL tests are explicitly skipped. Worker boundary tests use a labelled fake model and do not prove real inference. Playwright needs a working Chromium installation or `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH`. The cached Chromium binaries on the implementation machine failed at process launch; see acceptance evidence for the separate in-app-browser check.

## This implementation workspace

The .NET 10 SDK was installed privately under the parent task's `work/dotnet`, without changing the machine-wide .NET 9 installation. From this checkout, enable it for the current terminal if needed:

```powershell
$env:DOTNET_ROOT = (Resolve-Path ../../work/dotnet).Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
```

Other machines should install .NET 10 normally. Restore the pinned EF tool with `dotnet tool restore`; generate review SQL using `dotnet ef migrations script --idempotent --project backend/SmartCampus.Infrastructure --startup-project backend/SmartCampus.Api`.

For the new integrated Playwright specs, seed the local demo database, build/copy the current frontend into the API's wwwroot, run the API on port 5080, set `SMART_CAMPUS_E2E=1`, then run `npm run test:e2e`. These specs read the ignored demo receipt and never use real student credentials. Their discovery was verified, but native browser execution is blocked on this machine.
