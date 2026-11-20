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

Of the initial 20 records outside the Crossref DOI workflow, **13 now have source-identity lookup receipts listed above**. The remaining seven require comparable primary-source inspection: #19 is **not** part of the 20 because its linked URL includes a DOI; do not mix automated Crossref categories with source-review categories.

## Outstanding gates

The thesis has 91/91 mechanical in-text citation proximity candidates, but **none of these counters is an APA 7 certification**. Remaining: independently check the final printed author lists, journal/volume/page metadata, direct study-claim support and any early-online vs issue-year choices across all 91 entries. Maintain the distinct evidence boundaries for live AI/no-match calibration, approved hosted authenticated role testing, privacy/security sponsor scope and full university examination approval. University project approval and a campus security sponsor are **team-reported obtained** as documented in [the sponsor evidence register](APPROVAL_AND_SPONSORSHIP_EVIDENCE_2026-11-21.md); no formal signature or personal-data processing clearance has been fabricated.
