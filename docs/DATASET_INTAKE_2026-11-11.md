# Smart Campus — Drive datasets intake and evaluation boundary

**Inspection date:** 2026-11-11. **Status:** source inventory verified by Drive listing and metadata; payload contents have NOT been fully downloaded/hashed. This is research/evaluation data, **not** student-found items.

## Exact current inventory

The Drive folder contains `Dataset_File_Manifest.json` (52 payloads totalling **2,669,781,417 bytes / 2.67 GB**) and `Smart_Campus_Datasets_Guide.md`, plus the following subfolders:

- `Stanford Product Images`: **47 Parquet parts**, **100,883** retained product photographs, **22,290** product IDs and 12 product categories. The manifest reports train/test ID overlap of zero; do not reinterpret row indices as continuous IDs.
- `Reports and Object Photos`: **4 object-photo ZIPs** with **747** retained Open Images photos and one **Reports_Metadata_and_Checks.zip** with NYC maintenance (100,000 requests), Arabic synthetic ITSM (10,000), SNCF losses (1,148,442) and SNCF finds (705,747), associated notices, catalogs and checks.
- `AI_Experiments_2026-10-30`: historical experiment report/ZIP. It is not a deployed model or campus evaluation certificate.

The collection guide reports that the photos were screened using automated tools but **cannot guarantee** no persons/faces, license defects or semantic noise. Do not bulk publish these images on the app or import them to production `Items`, `Media` or `Embeddings`.

## Previously reported results (not rerun here)

The October 30 experimental report chose pretrained `google/siglip2-base-patch16-224` for text/image retrieval. On **200 independent photographed SOP bicycle product IDs**, its reported recall was **166/200 top-1 (83%)** and **179/200 top-5 (89.5%)**. A new small Arabic gallery check reportedly had **16/19** intended items at top-1. The author documents a serious open-set issue: an exploratory 0.90 cutoff would have falsely accepted **30/200** absent-item bicycle searches, while recovering only **84/200** correct top-1 matches. No safe threshold was approved.

For maintenance, OpenRouter Nemotron free was tried on synthetic/provisional tickets; source-label agreement is not verified fault-priority accuracy. Numeric ITSM priority labels must **not** be imported as campus physical-hazard decisions.

## Reproducible offline intake

Download all 52 payloads and the exact matching `Dataset_File_Manifest.json` into a local folder tree. Verify before analysis, **not** inside the 512 MB production container:

```sh
python scripts/verify_dataset_manifest.py \
  --manifest /path/to/Dataset_File_Manifest.json \
  --data-dir /path/to/downloaded-datasets \
  --json-output /path/to/dataset-integrity-result.json
```

The verifier checks complete per-file size and SHA-256 integrity; it exits nonzero when missing, duplicate or altered. It does **not** validate image contents, licensing, relevance, labels, model quality or representativeness. The ZIPs can stay archived during verification.

## Correct AI integration path

1. Obtain a **separate** worker host with sufficient memory/disk, acceptable terms and persistent model assets. The production ASP.NET container cannot host the CPU SigLIP worker on its existing 512 MB quota. Current `RetrievalService` requires a loopback worker; a separate host also needs an explicitly reviewed authenticated transport architecture and code changes (not an unrestricted public embedding endpoint).
2. Keep the existing pretrained pinned SigLIP model first. Do not claim fine-tuning was completed. Benchmark on group-separated test items, with one photo for the gallery and a distinct photo for the query; check the same item cannot occur in train and test or as a byte-identical query/gallery image.
3. Build an *independently checked* Arabic/English campus-like evaluation set spanning watches, wallets, keys, phones, bags, glasses etc. Include same-category distractors, mismatched descriptions, and known-absent items. Respect source licenses and privacy.
4. Measure recall@1, recall@5, precision and error rates of absent-item decisions separately for text-only and text-plus-image queries. Freeze the test split and a human-reviewed acceptance/calibration policy **before** configuring `Search__TextMinimumScore`, `Search__MultimodalMinimumScore` and `Search__CalibrationApproval`.
5. Index **only** genuine, permitted campus found-item records with the exact production model revision. Assert `Jobs` index completion and non-empty `Embeddings`; test access controls, media download, recovery grants and distinct actors end-to-end.
6. Keep reporting and physical staff decisions independent of the model. Don't treat similarity or a listing/product identity as proof of personal ownership.

## License and usage boundary

The collection guide identifies Open Images per-image attribution, non-commercial CC BY-NC use for the Arabic ITSM research material, a separate Stanford original notice, and SNCF ODbL with provenance caveats. Review actual file licenses and source terms before any distribution, publication or fine-tuning. Never commit the 2.67 GB payloads, personal data, provider secrets, generated vector databases or model weights to Git.

**No production dataset migration, AI training, calibration approval or deployment was performed during this intake.**


## Independent check of saved experiment evidence (2026-11-11)

The archived experiment ZIP was downloaded from the user's Drive and its saved 200-query retrieval outputs re-aggregated without running the model again. The stored figures reproduced exactly: top-1 **166/200**, top-5 **179/200**. With the correct item excluded, the reconstructed no-match probes were **70/200**, **30/200**, and **2/200** incorrectly accepted at exploratory score cutoffs 0.85, 0.90, and 0.95 respectively; original correct top-1 results retained at these cutoffs were **110/200**, **84/200**, and **34/200**. These are historical bicycle-product scores from the existing ZIP, not newly computed campus embeddings.

To re-check the actual saved ZIP, without extracting image archives:

```sh
python scripts/review_saved_retrieval.py /path/to/Smart_Campus_AI_Experiments_2026-10-30.zip
```

Never copy these exploratory cutoffs directly into either live modality threshold. A different domain, gallery size and independent positive/absent samples require a new frozen calibration set.
