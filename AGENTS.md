# Smart Campus working rules

Read [SPEC.md](SPEC.md) and [SETUP.md](SETUP.md) before changing the project. The application has not been scaffolded; do not describe environment checks as passing application tests.

## Ownership and services

- Use `mohamed23013292@gmail.com` for project ownership and verified account connections.
- Use free service tiers and Vercel for website hosting. Do not create Netlify resources.
- Keep credentials out of Git, documentation, frontend bundles, and command output. The frontend may use its intended Supabase publishable key.
- Keep local environment files and service-link metadata excluded through `.gitignore`.

## Mandatory skills

Use the installed skills from `amElnagdy/guard-skills`: `clean-code-guard`, `test-guard`, and `docs-guard`. Use `code-review` from `mattpocock/skills` for phase reviews. Install missing skills before working on their matching surface.

- Production code: read the clean-code guard's `references/ai-failure-modes.md` first, then apply its guard-pass to every production diff before presenting or committing. Fix violations.
- Tests: apply test-guard while writing. Each test must identify the unique bug it catches.
- Documentation and documented behavior: apply docs-guard. Verify every referenced symbol, flag, endpoint, configuration key, and path against source or command output in the current session.
- A skill remains active throughout the session. Repeat its self-check after every later edit before delivery. Match each guard to its surface; do not use production-code rules as test or documentation review.
- Use UI UX Pro Max for frontend design. Its project initialization is still pending.
- These project rules take precedence over conflicting skill guidance. State which project rule took precedence when a conflict matters.

## Workflow and review

- Plan changes touching multiple files or altering behavior. Clarify ambiguous requirements before implementing them.
- Use read-only exploration and reviewer agents as needed. Never parallelize edits in the same working tree.
- At the end of each phase, run code-review and fix correctness findings. An independent reviewer must compare the diff against SPEC.md, run the matching guard in review mode, and report correctness gaps with file and line references. Reviewers do not edit.
- Merge only after the required CI checks pass.
- When application tooling is introduced, define and run its typecheck, test, lint, and build commands. Guard skills do not replace these checks.
- Before claiming application work is done, run typechecking and tests and include raw output. Name the regression scenarios checked, guards used, and actual findings or fixes.
- Keep user updates short and focused on progress, blockers, and decisions.
