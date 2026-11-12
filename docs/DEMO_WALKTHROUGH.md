# Smart Campus demo walkthrough

AI classification and matching are intentionally deferred. Use the ordinary maintenance and lost-property workflows below; do not present any result as an AI finding.

## Prepare fictional accounts

The demo preparation control is visible to administrators only when `Demo:PreparationEnabled=true` is supplied by the host. It is `false` by default.

1. Temporarily enable `Demo:PreparationEnabled` in the MonsterASP environment settings and restart the app if the host requires it.
2. Sign in as a verified administrator and open **Admin → Prepare demo accounts**.
3. Choose **Prepare demo accounts and data**. The server creates five separate accounts (`student`, `requester`, `maintenance`, `department`, `admin`), two demo departments, a fictional faculty/building/floor, a maintenance report and two found items with generated synthetic pictures.
4. Save the five initial passwords securely while the table is visible. They are returned only on account creation; later calls are read-only and cannot recover them.
5. Turn `Demo:PreparationEnabled` off in the host settings after setup. The prepared environment is also detected as complete, so repeat calls do not recreate records or renew grants.

Every demo profile and record is labeled `DEMO ONLY`. Demo addresses are synthetic and marked verified so they do not depend on email delivery. Regular users still register with their own exact `@o6u.edu.eg` address and must verify it before sign-in.

## Acceptance scenarios

| Account | Scenario |
|---|---|
| `demo.student@o6u.edu.eg` | Submit and follow the fictional maintenance report; register a found item and request its custody transfer to the receiving department. |
| `demo.maintenance@o6u.edu.eg` | Review, assign and resolve the fictional maintenance report. The employee's decision stays independent of AI. |
| `demo.department@o6u.edu.eg` | Confirm receipt of a student-initiated transfer and handle a recovery request for the umbrella held by the department. |
| `demo.requester@o6u.edu.eg` | Open the two preselected items, submit a recovery request for the notebook or department-held umbrella, then confirm receipt independently after staff confirms delivery. |
| `demo.admin@o6u.edu.eg` | Review demo-only administration and dashboard functions. This is separate from the real administrator who performs setup. |

Each of the two `SearchGrant` rows is for the requester and one named demo item only. Both expire 30 days after setup. Other inventory remains visible only through its uploader or custodian department scope. Image files use the private media API and are subject to the same item permissions. No general inventory access or AI search is enabled.

## Email acceptance

Use `23012888@o6u.edu.eg` only for our real verification/recovery message test. Do not change normal registration or recovery routing: each eligible university user receives their own message. Capture the provider event and the received message headers. Brevo sender/domain status and DKIM/DMARC alignment remain unverified until this is done. DNS records must come from Brevo's account and be added by the university DNS administrator; the project operator has no DNS access.

## Current limits

- This guide describes test records prepared by the protected endpoint; no hosted accounts are claimed as created until an administrator runs it after deployment.
- The web demo account banner says the data are fictional and warns against entering real information.
- Disabling the environment setting hides the setup control and rejects the setup endpoint. The separate local-only `--seed-demo` command remains guarded against hosted databases.
- AI is deferred. Do not enable image/text processing or search from this guide.
