# Smart Campus — handoff for ChatGPT Work
Updated 2026-11-11. This brief is a **continuation checklist**, not permission to make unsafe changes or claim that the project is fully released.

## Start here
- Repository: https://github.com/MohamedTamer007/smart-campus
- Base implementation branch: `codex/backend-integration`
- **Draft change PR #2**: https://github.com/MohamedTamer007/smart-campus/pull/2 (head `codex/release-readiness-20261111`; do not merge until all checks and owner review pass).
- Public deployment: https://smart-campus-arm-b7d59.containers.snapdeploy.app/ ; existing SnapDeploy container (free plan with sleep/low memory).
- Supabase project: `mninpcrhqsaafoshjqpn`, application schema `smart_campus` (do not alter legacy `public` tables).
- Dataset folder: https://drive.google.com/drive/folders/1hss9jee4mdgKz-Whi4onN4VUg3pFuZ-Z
- Current model selection: pinned `google/siglip2-base-patch16-224` for local retrieval; OpenRouter Nemotron free for reviewed, non-sensitive maintenance suggestions. Model choice is not proof of a deployable service.

## Changes already prepared in PR #2
- Bilingual empty/error/loading state in receiving department select.
- Admin-only create-department/create-location screens, linked from `/admin`, using existing authorized API endpoints.
- Explicit required department scopes for staff grants.
- Typed frontend API adapter and UI regression tests.
- Offline dataset manifest integrity checker, tests, archived retrieval-stat verification.
- Dataset research and release-gate documentation.
- CI Ubuntu package-download retry/fallback; tests before dependency installation. Check all of these against latest commit and CI before proceeding.

## Current verified blockers, not guesses
Read-only inspection found 1 user, 0 roles, 0 departments, 0 locations, 0 scopes, 1 uploaded found-item, 1 media row, 0 retrieval embeddings, and a pending image-index job. An earlier PR CI run had 101 passing backend tests but failed at apt download: DNS resolution of `azure.archive.ubuntu.com` prevented `age` and `rclone` installation; backup/restore validation did **not** execute. Verify whether the amended latest CI has completed and whether further failures appear. Historical AI trials report 166/200 recall@1 and 179/200 recall@5 on SOP bicycles, but open-set false-acceptance is too high to approve a live threshold. Receiving email provider status `Sent` did not establish delivery to the university mailbox. The real account email was confirmed via the test link, not a delivered message.

## Autonomously do, in order
1. Inspect PR #2, latest GitHub Actions results/logs; fix genuine build/test/lint/CI defects on the **draft feature branch only**. Test and document exact evidence; avoid merging.
2. Validate admin directory UI with tests and accessibility. Verify it cannot create fictional entities or grant staff privileges by simply creating a department.
3. Review against `AGENTS.md`, `docs/ACCEPTANCE.md` and the source requirements; audit auth, CSRF, rate limits, media policy and permission tests.
4. Validate research dataset manifest/hash coverage in an isolated runtime with enough disk. Preserve licenses and personal-data screening. Do not place 2.67GB or pretrained weights into frontend bundle, Git, or the 512MB API container. Never upload product photos as real campus findings.
5. Prepare secure design/testing scripts for separate SigLIP worker hosting. Current `RetrievalService` only accepts loopback `http://127.0.0.1:8123`; a remote worker needs explicit secure authentication/TLS and threat-model review before production. Avoid unapproved paid hosts.
6. Build and run independently reviewed, group-separated, Arabic/English query/gallery/no-match benchmark; report recall@1/@5, false accept, false reject. Only the institution/project owner can approve final release threshold.
7. Test secure backup/restore in isolated DB and private-media restore, then realistic two-account/student-staff end-to-end journeys, mobile UX, auth recovery and notifications. Document which passes are synthetic vs live.
8. Produce a release checklist and PR review. Don't claim 100% readiness until all gates independently pass.

## Actions requiring explicit owner/institution input
- Identify the single already-verified `@o6u.edu.eg` account authorized as admin. Use existing `Bootstrap__Email` + documented one-time `--bootstrap-admin` process only after explicit confirmation and backup. Never elevate the sole user automatically.
- Supply authoritative departments (names/codes, `CanReceive`, `Maintenance`), faculty/building/floor directory and approved routing. Do not invent actual facilities.
- Have real staff enroll/verify; receive approval for each `department`/`maintenance` role and scope. No blanket grants.
- Verify an authorized sender domain/SMTP service and an actual university-recipient delivery; never store keys in code or chat logs.
- Select and authorize a worker host that meets resource requirements; confirm budget and operational availability. Existing SnapDeploy free container is not sufficient.
- Give release authorization for protected production SQL/permissions, a redeploy, opening controlled-release mode, any paid resource, or merging to `main`.

## Non-negotiable safety controls
- Preserve production users, items, email records and legacy AI trials. No destructive migration or anonymous data access changes.
- Supabase advisor flags RLS disabled on application schema. Current checked database grants **deny** `anon` and `authenticated` schema USAGE and direct Items SELECT. Audit effective grants before any RLS change; blanket RLS may break app.
- Don't expose private object photos in a public gallery. Don't interpret AI similarity as proof of ownership. Keep transfer and claimant confirmation separate and permission checked.
- Credentials belong in the provider UI/secret manager, not committed to GitHub.
- Read project instructions; never call unfinished steps complete or turn on a feature by bypassing safety gates.
