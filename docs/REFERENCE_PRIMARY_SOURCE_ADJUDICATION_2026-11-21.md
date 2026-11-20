# Smart Campus — primary-source bibliography adjudication (21 November 2026)

**Purpose:** Record evidence from the independent, public-source bibliography audit without pretending that a DOI/title string match proves scholarly attribution or full APA 7 correctness.

## Actual Crossref execution

- Workflow: [Scholarly DOI metadata screen #44832035480](https://github.com/MohamedTamer007/smart-campus/actions/runs/44832035480), success; source commit `90c9cb975855e752a8eae6a3adc16339cd2111b8`.
- Input: 91 registered academic references. 70 bibliography strings include printed DOI URLs; one further record (#19) has a DOI only in the linked source. **71 Crossref DOI lookups** were attempted; 20 entries were marked `NON_DOI_SOURCE_REVIEW`.
- **Raw program findings:** 66 `LIKELY_ALIGNED`, 4 `REVIEW_METADATA`, 1 `CROSSREF_NOT_VERIFIED`; all 91 have a recorded result. The authoritative JSON artifact is `crossref-metadata-screen` on the run, artifact ID `14677570749`, SHA-256 `54ea2f644127f78d23aae21da124d08a7df3b057eba46542c08c2553d27ac5a5`. Artifact retention: 14 days.
- This is a **publisher/depository metadata screening**, not 66 academically certified claims or a report of 100% APA 7 formatting.

## Five Crossref flags individually investigated

| Bibliography # | Original machine flag | Secondary/official finding | Adjudication |
| ---: | --- | --- | --- |
| 16 — Babenko & Lempitsky (2015) | `REVIEW_METADATA`; Crossref first-author string `Yandex` | The official [IEEE ICCV open-access proceedings](https://openaccess.thecvf.com/content_iccv_2015/html/Babenko_Aggregating_Local_Deep_ICCV_2015_paper.html) explicitly attributes *Aggregating Local Deep Features for Image Retrieval* to **Artem Babenko and Victor Lempitsky (2015)** | **Source identity corroborated; Crossref name field malformed**. Do not replace the authors with Yandex |
| 41 — Ghazal et al. (2016) | `REVIEW_METADATA`; Crossref author `Ghazal, et. al.` | [Abu Dhabi University repository](https://repository.adu.ac.ae/items/50e44846-18b5-4d8b-a629-e9e85abec7c7/full) attributes the same lost-object retrieval paper to **Ghazal, Alkhalil and Ali** and links DOI `10.12785/IJCDS/050107` | **Source identity corroborated; abbreviated Crossref first-author string** |
| 68 — Nwahanye (2021) | `REVIEW_METADATA`; Crossref author array absent | [Original publisher ARC Journals](https://arcjournals.org/article/6740) lists **Nwahanye Emmanuel**, the same 2021 Camtel customer-retention study and DOI `10.20431/2349-0349.0903002` | **Source identity corroborated; Crossref author metadata incomplete** |
| 72 — Sakai et al. (2024) | `CROSSREF_NOT_VERIFIED` / 404 for DOI `10.48550/arXiv.2410.23528` | [Original arXiv record](https://arxiv.org/abs/2410.23528) directly displays *Large Language Models for Patient Comments Multi-Label Classification*, **Sakai et al.**, first submission **31 October 2024** | **Source identity corroborated as a 2024 preprint**; no Crossref deposition verified. Check whether the university bibliography policy prefers a later published version |
| 82 — Stephen et al. | `REVIEW_METADATA`; registered 2019 while Crossref indexes 2020 | [University of Strathclyde institutional repository](https://pureportal.strath.ac.uk/en/publications/extracting-distribution-network-fault-semantic-labels-from-free-t/) indexes the *Extracting Distribution Network Fault Semantic Labels from Free Text Incident Tickets* work as **2019** with the same DOI; Crossref reports **2020** as published bibliographic year | **Year discrepancy not silently resolved.** An early-online 2019/issue 2020 distinction is plausible. Verify final IEEE issue metadata and the faculty's APA year rule before changing 2019 in the thesis |

The primary references above resolve why the automated **four first-author/year flags and one Crossref-absent result** appeared; they are **not** a line-by-line manual review of all claims that cite them. At least the #82 edition/year decision still needs independent editorial approval. No DOI or author names were blindly overwritten.

## Primary-source crosscheck of non-Crossref bibliography entries

The following **13** entries have source pages from the original publishing association, university or institutional repository that identify the cited work. The check is a **title/first-author/year identity screening**, not an independent full author-order, venue, quotation, citation-style and experimental-claim audit.

| # | First author and year | First-party record |
| ---: | --- | --- |
| 1 | Abdelali (2021), QADI | [ACL Anthology](https://aclanthology.org/2021.wanlp-1.1/) |
| 17 | Balabel (2020), CSCS | [ACL Anthology](https://aclanthology.org/2020.lrec-1.489/) |
| 20 | Blümel & Zaki (2022), complaint prioritization | [HICSS / AIS eLibrary](https://aisel.aisnet.org/hicss-55/da/service_analytics/4/) |
| 28 | Dahou (2016), Arabic CNN sentiment | [ACL Anthology](https://aclanthology.org/C16-1228/) |
| 30 | Dutta (2020), building operations thesis | [Carleton University institutional listing](https://carleton.ca/dbom/alumni-thesis/) |
| 33 | Elfardy & Diab (2012), code switching | [ACL Anthology](https://aclanthology.org/C12-2029/) |
| 40 | Ghaddar (2022), Arabic NLP | [ACL Anthology](https://aclanthology.org/2022.emnlp-main.205/) |
| 47 | Hamed (2025), code-switching survey | [ACL Anthology](https://aclanthology.org/2025.coling-main.307/) |
| 50 | Inoue (2021), Arabic pretrained models | [ACL Anthology](https://aclanthology.org/2021.wanlp-1.10/) |
| 64 | Molina (2016), code-switched language identification | [ACL Anthology](https://aclanthology.org/W16-5805/) |
| 75 | Shazal (2020), Arabizi | [ACL Anthology](https://aclanthology.org/2020.wanlp-1.15/) |
| 83 | Tian (2015), bug priority | [Monash University repository](https://research.monash.edu/en/publications/automated-prediction-of-bug-report-priority-using-multi-factor-an/) |
| 88 | Zaidan & Callison-Burch (2011), Arabic commentary | [ACL Anthology](https://aclanthology.org/P11-2007/) |

**Confirmed metadata gap:** Source #83 (Tian et al. 2015) contains original publisher DOI `10.1007/s10664-014-9331-y` on the Monash institutional record, but the team's registry cites the university repository URL rather than visibly printing this DOI. A final APA pass can add the verified DOI without removing the institutional citation trail. Do not confuse this with an unverified DOI.

Of the initial 20 records outside the Crossref DOI workflow, **19 now have original-source identity lookup receipts** across the two tables below. The remaining case #53 has third-party copies but lacks independently authenticated Cal Poly Pomona institutional provenance; #19 is **not** part of these 20 because its source link includes a DOI.

## Additional original-source confirmations and the one outstanding non-DOI item

Six more non-Crossref records were checked against original publishers or an author-controlled source. Combined with the 13 above, **19/20 original non-DOI-source records** now have independent first-author/title/year source-identity support.

| # | First author and year | Original source and result |
| ---: | --- | --- |
| 48 | Hmeidi et al. (2008), full-word Arabic KNN/SVM | [Elsevier / ScienceDirect publisher record](https://www.sciencedirect.com/science/article/abs/pii/S1474034607000742) confirms title, three authors, 2008, volume 22(1), pp. 106–111 |
| 58 | Li et al. (2023), energy-vehicle online reviews and complaints | [Elsevier / ScienceDirect publisher record](https://www.sciencedirect.com/science/article/pii/S2352484723001464) confirms first author Qiaoxing Li, 2023 and title |
| 78 | Singh, Bhatia & Saha (2024), financial complaint severity | [IEEE Xplore publisher record](https://ieeexplore.ieee.org/abstract/document/10047986) confirms title and 2024 issue of *IEEE Transactions on Computational Social Systems* |
| 79 | Singh, Dey, Singha & Saha (2022), multimodal complaints | [AAAI primary proceedings](https://ojs.aaai.org/index.php/AAAI/article/view/21476) confirms authorship, title and 2022 |
| 81 | Stadnicka, Antosz & Ratnayake (2014), machine maintenance priorities | [Elsevier / ScienceDirect publisher record](https://www.sciencedirect.com/science/article/pii/S0925753513002531) confirms title and author/year |
| 86 | Xuan et al. (2015), software bug triage | [Original author-hosted IEEE paper](https://jifeng-xuan.com/page/paper/tkde_15.pdf) confirms Jifeng Xuan and coauthors, title, 2015 issue; bibliographic DOI `10.1109/TKDE.2014.2324590` is printed on the paper |

**Remaining source-provenance gate — #53 (Harkamal Kaur, 2017):** A [publicly mirrored project report](https://www.scribd.com/document/428840626/KaurHarkamal-Project2017-pdf) has a 2017 Cal Poly Pomona master's-project title/signature page naming Kaur. Other third-party mirrors and academic references repeat the source, but an original **Cal Poly Pomona institutional repository record or authenticated submitted file** was not independently located in this check. The work appears real but must remain **SECONDARY-COPY CORROBORATED; INSTITUTIONAL PROVENANCE UNVERIFIED** rather than certified by a third-party upload.

This raises first-source identity screening of non-Crossref records to **19/20**, with one original institutional-source verification still open. It does not validate experimental data or 91/91 APA 7 accuracy.

## Outstanding gates

The thesis has 91/91 mechanical in-text citation proximity candidates, but **none of these counters is an APA 7 certification**. Remaining: independently check the final printed author lists, journal/volume/page metadata, direct study-claim support and any early-online vs issue-year choices across all 91 entries. Maintain the distinct evidence boundaries for live AI/no-match calibration, approved hosted authenticated role testing, privacy/security sponsor scope and full university examination approval. University project approval and a campus security sponsor are **team-reported obtained** as documented in [the sponsor evidence register](APPROVAL_AND_SPONSORSHIP_EVIDENCE_2026-11-21.md); no formal signature or personal-data processing clearance has been fabricated.


## Supplemental source corrections — 21 November 2026

- **#83 — Tian et al. (2015), exact DOI:** The [Monash University institutional publication record](https://research.monash.edu/en/publications/automated-prediction-of-bug-report-priority-using-multi-factor-an/) and the [published author-manuscript PDF](https://cs.uwaterloo.ca/~cnsun/public/publication/emse14/emse14.pdf) identify `10.1007/s10664-014-9331-y`, with `Empirical Software Engineering`, 20(5), 1354–1383 (2015 final issue). The original Google Docs bibliography and `REFERENCE_AUDIT_2026-11-20.md` were aligned to the canonical DOI rather than only the Monash landing URL. This is a verified one-entry metadata correction.
- **Chapter 2 citation repair — Mo et al. (2020):** The thesis's title-only description of *Automated staff assignment for building maintenance using natural language processing* was replaced by the correct first-author/year attribution **Mo et al. (2020)**, corroborated by the [Elsevier publisher page](https://www.sciencedirect.com/science/article/abs/pii/S0926580519314529) and [Hong Kong Polytechnic University publication record](https://research.polyu.edu.hk/en/publications/automated-staff-assignment-for-building-maintenance-using-natural/). A mixed parenthetical APA-style reference to Wagner et al. (2016) was corrected.
- **Abstract claims aligned:** It now distinguishes the university's reported project approval/sponsorship from **still-required independent operational authorization, approved data handling and evaluation**. Wording changes do not assert that a final faculty examiner signed off or that real student-data handling was cleared.
- **#82 — Stephen et al. remains a controlled decision:** The [2019 advance-online Strathclyde institutional record](https://pureportal.strath.ac.uk/en/publications/extracting-distribution-network-fault-semantic-labels-from-free-t/) and final [2020 IEEE 35(3), 1610–1613 issue references](https://doi.org/10.1109/TPWRD.2019.2947784) are consistent with distinct online/issue dates. Do not silently change bibliography year across all references/Chapter 2 until the faculty's required APA edition convention is confirmed.
- **#53 — Kaur (2017) remains institutional-unverified:** Repeated external searches located university ScholarWorks landing pages and third-party mirrors of the project report, but no authenticated item-specific Cal Poly Pomona repository record. Keep the provenance limitation visible.

This addendum is not a claim of a full 91-item APA 7 review or original-paper experiment reproduction.
