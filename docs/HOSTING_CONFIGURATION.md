# SmartCampus configuration without repeated MonsterASP environment edits

The Windows IIS application reads:
1. `appsettings.json` (checked-in core defaults).
2. `smartcampus.defaults.json` (checked-in, non-secret operational settings).
3. `appsettings.Local.json` (development only; never published).
4. Hosting environment variables (highest priority).

## Routine changes: just push code

Edit `backend/SmartCampus.Api/smartcampus.defaults.json` for **non-secret** defaults such as rate limits and safe feature defaults, then push to `codex/release-readiness-20261111`. The GitHub Actions workflow compiles both applications and deploys the file through WebDeploy. No separate Environment variables screen edit is needed for these settings **unless the same setting was already overridden by a host environment variable**.

The deployment script rejects known secret-style key names in the committed defaults. Do not add university email lists, passwords, certificates, API keys, connection strings or approval references here. New features should read `IConfiguration` with sensible fail-closed defaults where they can.

## Credentials that cannot be guessed or committed

Keep the existing MonsterASP environment variables for the database connection, private storage key, authenticated email sender/provider credentials, Data Protection secrets when applicable, and provider API keys. These do not disappear when the code changes. New external providers and rotated credentials still require secure configuration; GitHub's four WebDeploy secrets authenticate the deployment and do **not** automatically become runtime environment variables.

`PublicOrigin`, `AllowedHosts`, `Hosting:Platform` and `DataProtection:Store` are tied to the selected host: configure them once correctly, and keep them stable. The app validates required production secrets and aborts safely rather than inventing keys or falling back to insecure local storage.

MonsterASP's published documentation describes control-panel Environment variables and restarting the app after changing them. It does not document a supported environment-variable write API for this plan. Avoid building fragile automation that stores the MonsterASP dashboard password in the repository or scrapes authenticated panel forms.

## Check outcomes

A successful GitHub Actions deployment establishes build, WebDeploy and API liveness; it is not proof that email, AI or private media have passed their end-to-end acceptance gates. Monitor the Actions run on unexpected config changes.
