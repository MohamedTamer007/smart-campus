# Smart Campus environment setup

Verified on 2026-10-21. Required owner: `mohamed23013292@gmail.com`.

## Vercel

- Account: `mohamed23013292`; verified account email matches the required owner.
- Team: `mohaned9` (`team_ZYNs1kNCGWv2X67AwyoyN81D`), Hobby plan.
- Project: `smart-campus` (`prj_02r6oM2C29H2qYFMH0z66sl6JsKG`), Vite framework.
- Reserved domain: <https://smart-campus-brown-seven.vercel.app>. No application deployment has been created.
- GitHub repository `mohamed23013292/smart-campus` is linked; production branch is `main`. Git-triggered deployments are disabled in `vercel.json` until the application has a working build.
- Environment variables configured as encrypted values for development, preview, and production: `VITE_SUPABASE_URL` and `VITE_SUPABASE_PUBLISHABLE_KEY`.
- The frontend receives a Supabase publishable key. No Supabase management token, database password, or privileged API key was added to Vercel.

## Supabase

- Reused the existing project `mohamed23013292's Project` (`mninpcrhqsaafoshjqpn`) in the Smart Campus organization (`xtqttjbdkmxvrsmnqrzb`).
- Organization membership includes the required owner email.
- Region: `eu-west-1`. Project status: `ACTIVE_HEALTHY`.
- API URL: <https://mninpcrhqsaafoshjqpn.supabase.co>.
- Auth site URL: <https://smart-campus-brown-seven.vercel.app>.
- Additional auth redirect pattern: `http://localhost:5173/**`.
- Email confirmation remains required. Custom SMTP is not configured; delivery to the university inbox has not been tested.
- A read-only SQL query returned PostgreSQL `17.11` and zero public tables. This setup created no application schema or storage buckets; existing storage buckets and other database schemas were not inspected.
- Auth health request returned HTTP 200 with GoTrue `v2.197.0`.
- The supplied database password has not been tested using a direct database connection.

## GitHub

After the user removed the previous GitHub connection, CLI authentication with the supplied token returned `mohamed23013292`. The account's primary email is `mohamed23013292@gmail.com`, verified by GitHub.

Private repository: <https://github.com/mohamed23013292/smart-campus>. Vercel's repository-link API confirmed the same owner and repository. Git identity is configured locally for this repository as `mohamed23013292` with the required owner email.

## Hugging Face

- User-provided account name: `MohamedTamerh`. Account ownership was not independently verified.
- On 2026-10-23, GitHub's secret-list command confirmed the repository secret `HF_TOKEN` exists. Its value was not retrieved, and token validity and permissions have not been tested.
- The GitHub Actions secret does not automatically configure cloud-task or application-server credentials. AI hosting and model memory requirements remain unverified.

## Credentials and remaining setup

No credentials are included in this file. Privileged credentials were not committed, added to frontend configuration, or configured as managed cloud secret bindings. The frontend configuration contains only the intended public connection URL and publishable key. Store replacement privileged credentials in the cloud environment's secret configuration before relying on a new runtime. The user reported a 30-day token lifetime; expiration dates were not independently verified.

Remaining work: establish SMTP delivery and select AI hosting after measuring model memory requirements. CI, application deployment, schema migrations, application tests, typechecking, and the frontend design skill's project initialization remain pending because no application code exists yet.
