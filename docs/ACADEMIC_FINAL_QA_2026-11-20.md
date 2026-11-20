# Smart Campus — Academic submission and evidence QA (20 November 2026)

**Scope:** [Native thesis](https://docs.google.com/document/d/1UWZoMqGwvjm_V_H6efziZfP8bCW1nZLODHFAOrRQu1Q/edit), this repository, the 20 November application release and its independently named CI/deployment evidence. This is a traceable academic QA record, **not** an institutional acceptance certificate, penetration test, or production-data authorization.

## Deliverable and layout review

- The native Google Doc has **six numbered chapters**, **five appendices (A–E)**, **39 inline images**, **34 native tables** and **91 linked bibliographic entries**.
- The current native PDF export contains **142 pages**. Chapter starts verified against extracted PDF pages: Chapter 1 page **11**, Chapter 2 **17**, Chapter 3 **29**, Chapter 4 **75**, Chapter 5 **96**, Chapter 6 **108**, References **117**, Appendices **123** (A 123, B 128, C 134, D 138, E 139). The existing contents page agrees at these landmarks. Page numbering must be rechecked after subsequent content changes.
- Representative diagram-only PDF pages 50, 54, 58 and 67–70 were visually inspected; they contain diagrams rather than accidental blank pages. This spot check cannot certify every figure's readability at print scale or accessibility.
- All 91 bibliography entries retain at least one clickable source link. Six additional references were replaced with source-verified author/year/publication details and link styling in the native thesis. The remaining entries are **not certified as APA 7**: a source link does not prove accurate authorship, bibliographic metadata, matching in-text attribution, DOI, or APA formatting. See [APA reference list guidance](https://apastyle.apa.org/style-grammar-guidelines/references/basic-principles).

## Evidence ledger — three different claims

| Area | Present in source or document | Controlled automated evidence | Hosted/independent evidence | Honest boundary |
| --- | --- | --- | --- | --- |
| Architecture, APIs, database, requirements | Yes: [architecture](ARCHITECTURE.md), [OpenAPI](openapi.json), [coverage](REQUIREMENTS_COVERAGE_2026-11-14.md) | Real database and browser tests | PR #21 deployment identity and public liveness | Source coverage is not live role acceptance |
| Accounts, roles, maintenance, custody and recovery | Implemented | Latest independently inspected successful [main CI 44805517934](https://github.com/MohamedTamer007/smart-campus/actions/runs/44805517934): 280 backend passed, 4 skipped; 140 frontend passed; 51 browser regressions; 9 integrated journeys | Dated historical hosted API observations in [acceptance log](ACCEPTANCE.md) and thesis Appendix C | No full user-journey acceptance for 20 November deployed SHA |
| Public application package | Windows publish, WebDeploy and build-info marker | [PR #21 package](https://github.com/MohamedTamer007/smart-campus/actions/runs/44781998433) | [Automatic deployment 44783186261](https://github.com/MohamedTamer007/smart-campus/actions/runs/44783186261) matched `eb3c62c2c743ce792a53f311ce97c3751d422437` and live health at probe time | Does not establish current uptime, private storage authorization or real-user acceptance |
| AI classification | OpenRouter provider integration, non-AI fallbacks, explicit staff approval | Boundary tests with provider substitutes | Historical team-reported synthetic experiment; raw 42-case package not found | No independent accuracy certification or permission for live personal-data inference |
| Lost-item retrieval | Gemini integration, embeddings, private candidate gates, keyword fallback | Tests of ranking/authorization/versions | Six positive cases in [small fictional Gemini experiment](GEMINI_RETRIEVAL_RESULTS_2026-11-14.md) had Top-1 and Top-5 of 6/6 | Same-pixel image positives; no-match rejection **Not Evaluated** |
| Security and dependencies | Source controls and [security review](SECURITY_REVIEW.md) | [CI 44805517934](https://github.com/MohamedTamer007/smart-campus/actions/runs/44805517934) reported zero npm installation/production-audit vulnerabilities and passed NuGet audit | No independent live pen test/storage-policy review | Do not claim production security approval |
| Privacy/hosting | Safe defaults for external processing; synthetic test records | Isolated storage-policy fixture tests | MonsterASP Free public preview only | **No real student identities, credentials, photos or records** permitted on that Free host |
| Human accessibility, usability and scale | Design decisions and scripted browser flows documented | Browser automation and small test/probe history | No documented human-screen-reader or campus-scale acceptance | Institutional/human measurements outstanding |

The CI above belongs to documentation-only commit `75bcc637521050c72b5b6c42c27c8de836bab3f0`. It must not be represented as a new deployed application release. Any later main-push CI must be linked with its actual outcome before upgrading these claims.

## Evidence needed before a defensible final acceptance statement

1. **Student/staff/admin acceptance:** On an approved host and identified build SHA, use distinct authorized *fictional* test identities; capture expected versus observed states for verification, maintenance, custody/claim, search permissions, invalid uploads, rejection, expiry, concurrency and recovery. Retain redacted result traces and screenshots; record operator, timestamp and exact build SHA.
2. **Original O-03 registration failure:** Reproduce original synthetic image/field scenario, record HTTP status and stable validation code, verify image decoder/dimensions, server trace and storage outcome. The later successful attempt and improved messages do not identify original cause.
3. **Independent AI evaluation:** Obtain the actual raw labeled cases, outputs, model revision, prompts, split definitions, sample provenance and reproducibility script. Perform prospective blinded review, repeated-photo-separated retrieval, distractors and no-match testing. Follow [AI evaluation protocol](AI_EVALUATION_REPRODUCTION_PROTOCOL.md).
4. **Real email integration:** Approved sender with valid SPF/DKIM and authorized test inbox; run verification, expiry/replay and recovery; distinguish API/provider acceptance from inbox receipt. Never publish secrets or tokens in logs.
5. **Live private media/storage and backups:** Verify actual deployed bucket/prefix policies and cross-account denial; prove off-host encrypted backup **and** isolated restore from the chosen production host, not only CI fixtures.
6. **Accessibility/UX and performance:** Dated keyboard-only and screen-reader sessions, Arabic RTL and mobile device review, human usability tasks, plus workload size, hardware, P50/P95/P99 latency and error rate under representative concurrent load.
7. **Institutional requirements and authorization:** Obtain 2026–2027 faculty template/rubric, approved directory/roles, retention/privacy terms, review signatures and host approval. A historical 2023–2024 template is not a new approval.
8. **Reference audit:** Verify complete authors (ordered as published), publication year/title/venue/pages/DOI for all 91 records, APA 7 formatting and exact in-text/reference correspondence. Do not invent metadata or cite a homepage as if it were a particular paper.

These are **external evidence gates**, not missing paragraphs that can safely be filled with model-generated results. The document's academic validity depends on stating unfinished evaluations as unfinished.

## Review and submission packet

- Thesis: [Smart Campus Documentation](https://docs.google.com/document/d/1UWZoMqGwvjm_V_H6efziZfP8bCW1nZLODHFAOrRQu1Q/edit)
- Application and reproducibility: [repository main](https://github.com/MohamedTamer007/smart-campus)
- Requirement-to-implementation index: [coverage matrix](REQUIREMENTS_COVERAGE_2026-11-14.md)
- API and architecture: [OpenAPI](openapi.json), [API handoff](API_HANDOFF.md), [architecture](ARCHITECTURE.md), [database](DATABASE.md)
- Visual diagrams and source disclosure: [diagram source inventory](diagrams/README.md), thesis Appendix D
- Evidence and limits: [acceptance](ACCEPTANCE.md), [release matrix](RELEASE_EVIDENCE_MATRIX_2026-11-20.md), [release status](RELEASE_STATUS_2026-11-20.md), [security review](SECURITY_REVIEW.md)
- AI protocol: [AI evaluation reproduction](AI_EVALUATION_REPRODUCTION_PROTOCOL.md)
- Current verification versus hosted acceptance: [PR #21 deployment](https://github.com/MohamedTamer007/smart-campus/actions/runs/44783186261) and [last independently inspected passing full CI](https://github.com/MohamedTamer007/smart-campus/actions/runs/44805517934)

## Quality-control rule

For every assertion, identify the **subject, source commit/model revision, environment, exact method and dated primary artifact**. Mark `PASS` only for a real executed check; otherwise record `NOT RUN`, `NOT EVALUATED` or `AWAITING APPROVAL`. Never convert synthetic examples, test doubles, unverified claims or draft plans into observed results. No passwords, bearer tokens, private data or provider keys belong in submitted evidence.
