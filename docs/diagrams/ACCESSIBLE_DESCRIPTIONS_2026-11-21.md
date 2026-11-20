# Smart Campus — Chapter 3 figure descriptions for review (21 November 2026)

These source-checked **proposed long descriptions** supplement the low-resolution images embedded in the academic thesis. They are text alternatives for reviewers and a basis for assigning native alt text in the approved deliverable. **They are not yet embedded as screen-reader alt text in the original Google Doc, nor human accessibility-certified.** The embedded figures remain authoritative for visual appearance. The Mermaid code in this folder is a semantic reconstruction, not an original design master.

## Figure 3.1 — Development stages and change control (printed PDF p. 30)

A seven-stage process starts with *Problem study* (literature review and research workbook), followed by *Requirements* (planning and requirements version 1.0), *Visual design* (18 reviewed boards), *Integration* (API contracts and data definitions), *Implementation* (frontend, backend, and AI owners), *Verification* (functional, AI and quality checks), and *Change control*. Arrows follow these steps; an additional arrow returns **approved** changes from stage seven to requirements. The heading emphasizes that each review can feed approved changes back into requirements. This is a project process model, not a measured runtime execution graph.

## Figure 3.2 — Maintenance report flow (printed PDF p. 31)

Six connected steps, read left-to-right along the top row and then right-to-left along the bottom row: (1) a verified reporter submits a description and location; image optional; (2) the backend stores the report, returns a reference and tracking link; (3) the backend may produce advisory fault type, priority and risk shown to **staff and admin**, not the reporter; (4) authorized staff review and assign within scope; (5) staff update statuses and supply a written reason for rejection; (6) the reporter tracks only their own reports and may edit or cancel before handling. The five displayed statuses are Submitted, Under review, Being repaired, Resolved and Rejected. The backend enforces transitions and permissions. An unavailable AI provider must not prevent the report from being stored.

## Figure 3.3 — Lost-and-found recovery and delivery flow (printed PDF p. 33)

Six connected steps, top row left-to-right then bottom row right-to-left: (1) a verified finder registers an item with image, description, place, date and custody choice; (2) a verified potential owner searches privately using a required description and optional photo; (3) the requester submits a recovery request, potentially with a message; (4) the confirmed custodian approves **at most one** request, reserving the item and leaving other requests pending; (5) both custodian and requester independently confirm handover/receipt, and **one confirmation alone does not close** the item; (6) the system closes the item and related pending claims after both confirmations. Candidate matches are not proof of ownership. The actual API gates and status behavior, not this picture, are the executable source of truth.

## Application screenshot — private lost-item search form (printed PDF p. 92)

Screenshot of the English desktop search page. Navigation includes Home, My requests, Arabic language switch and Account. The form title is *Search for a lost item*. A required, focus-highlighted description field contains a fictional example, *blue water bottle with a white cap*. The optional previous-item-photo file chooser is empty. Help text discourages personal information and says images are previewed locally before submission. A notice explains that a suggested match does not prove ownership and only authorized results will be displayed. This image documents a specific screen state; it is not a WCAG test.

## Acceptance before replacing print images or asserting accessibility

- A human academic reviewer must compare each description with the actual signed-off image and source requirements.
- Assign native image alt text in the thesis document or an accessible PDF alternative, then test reading order with a screen reader. A separate markdown file alone is **not** a screen-reader solution.
- Replace raster originals only with approved, semantically identical and legible native/vector artwork. Re-export and check page numbers, in-text references, caption positions, contrast and print scale.
- Do not infer a deployed security/property claim from a documentation diagram or sample UI screenshot.
