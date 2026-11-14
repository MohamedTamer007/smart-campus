#!/usr/bin/env python3
"""Independently recompute historical SigLIP retrieval trial metrics.

Read only the saved experiment JSON members of the archived trial ZIP.
This is descriptive evidence, NOT a production calibration approval.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from zipfile import ZipFile

BASE = "smart-campus-ai-experiment/"


def analyze(archive: Path) -> dict:
    with ZipFile(archive) as source:
        def get(member: str):
            return json.loads(source.read(BASE + member))
        trial = get("results/siglip2_heldout_200.json")
        manifest = get("data/heldout_retrieval_manifest.json")
        historical = get("results/no_match_stress.json")

    rows = trial["outputs"]
    gallery, queries = manifest["gallery"], manifest["queries"]
    gallery_ids = [item["id"] for item in gallery]
    query_ids = [item["id"] for item in queries]
    if (len(rows) != 200 or len(gallery) != 200 or len(queries) != 200 or
        len(set(gallery_ids)) != 200 or len(set(query_ids)) != 200 or
        set(gallery_ids) != set(query_ids)):
        raise ValueError("unexpected/duplicate heldout item groups")
    query_paths = {item["id"]: item["path"] for item in queries}
    gallery_paths = {item["id"]: item["path"] for item in gallery}
    if any(query_paths[item_id] == gallery_paths[item_id] for item_id in gallery_ids):
        raise ValueError("an identical query/gallery image path is present")

    outputs = {str(item["item_id"]): item for item in rows}
    if len(outputs) != 200 or set(outputs) != set(gallery_ids):
        raise ValueError("trial outputs do not match heldout item groups")
    if any(row["query_path"] != query_paths[key] for key, row in outputs.items()):
        raise ValueError("query path differs from frozen heldout manifest")

    top1 = sum(row["rank"] == 1 for row in rows)
    top5 = sum(row["rank"] <= 5 for row in rows)
    if top1 != trial["summary"]["top1_correct"] or top5 != trial["summary"]["top5_correct"]:
        raise ValueError("stored retrieval totals disagree with candidate rows")

    probes = []
    for cutoff in (0.85, 0.90, 0.95):
        absent_accepted = 0
        correct_retained = 0
        for row in rows:
            impostors = [float(candidate["score"]) for candidate in row["top5"]
                         if str(candidate["item_id"]) != str(row["item_id"])]
            if not impostors:
                raise ValueError("no sampled impostor for a query")
            if max(impostors) >= cutoff:
                absent_accepted += 1
            if (row["rank"] == 1 and row["top5"][0]["item_id"] == row["item_id"]
                    and float(row["top5"][0]["score"]) >= cutoff):
                correct_retained += 1
        probe = {"cutoff": cutoff, "absent_incorrectly_accepted": absent_accepted,
                 "original_correct_top1_retained": correct_retained, "cases": len(rows)}
        original = next((p for p in historical["threshold_probes"]
                         if abs(float(p["cutoff"]) - cutoff) < 0.000001), None)
        if original is None or any(original.get(k) != value for k, value in probe.items()):
            raise ValueError("recomputed open-set probe disagrees with saved report")
        probes.append(probe)
    return {
        "source": "archived 2026-10-30 SOP bicycle experiment, NOT a campus benchmark",
        "queries": len(rows),
        "recall_at_1": top1 / len(rows),
        "recall_at_5": top5 / len(rows),
        "top1_count": top1,
        "top5_count": top5,
        "exploratory_no_match_cutoffs": probes,
        "production_calibration_approved": False,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="Smart_Campus_AI_Experiments_2026-10-30.zip")
    args = parser.parse_args()
    if not args.archive.is_file():
        parser.error("experiment archive not found")
    try:
        result = analyze(args.archive)
    except (ValueError, KeyError, OSError) as exc:
        parser.error(str(exc))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
