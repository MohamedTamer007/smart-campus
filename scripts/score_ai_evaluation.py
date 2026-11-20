#!/usr/bin/env python3
"""Evaluate frozen, adjudicated Smart Campus AI exports offline; never calls a provider."""
import argparse
import csv
import hashlib
import json
from collections import defaultdict
from pathlib import Path

ABSTAIN = "__ABSTAIN__"


def table(path, columns):
    with Path(path).open(newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        if not columns.issubset(reader.fieldnames or []):
            raise ValueError(f"{Path(path).name}: required CSV columns missing")
        rows = list(reader)
    if not rows:
        raise ValueError(f"{Path(path).name}: no observations; no score is available")
    return rows


def keyed(rows, key):
    result = {}
    for r in rows:
        k = r[key].strip()
        if not k or k in result:
            raise ValueError(f"missing/duplicate {key}")
        result[k] = r
    return result


def digest(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as f:
        for chunk in iter(lambda: f.read(1048576), b""):
            h.update(chunk)
    return h.hexdigest()


def metrics(actual, predicted):
    labels = sorted(set(actual) | (set(predicted) - {ABSTAIN}))
    if not actual or not all(actual):
        raise ValueError("missing adjudicated labels")
    summary = {}
    for label in labels:
        tp = sum(a == label and p == label for a, p in zip(actual, predicted))
        fp = sum(a != label and p == label for a, p in zip(actual, predicted))
        fn = sum(a == label and p != label for a, p in zip(actual, predicted))
        precision = tp / (tp + fp) if tp + fp else 0.0
        recall = tp / (tp + fn) if tp + fn else 0.0
        summary[label] = {"support": actual.count(label),
                          "precision": precision, "recall": recall,
                          "f1": 2 * precision * recall / (precision + recall)
                          if precision + recall else 0.0}
    return {"count": len(actual),
            "accuracy_including_failures": sum(a == p for a, p in zip(actual, predicted)) / len(actual),
            "macro_f1": sum(m["f1"] for m in summary.values()) / len(summary),
            "per_class": summary}


def maintenance(cases, predictions):
    gold = table(cases, {"case_id", "split", "description_id",
                         "annotation_status", "gold_fault_type",
                         "gold_priority", "gold_risk"})
    outs = table(predictions, {"run_id", "case_id", "model_revision",
                               "pred_fault_type", "pred_priority",
                               "pred_risk", "provider_status"})
    known = keyed(gold, "case_id")
    preds = keyed(outs, "case_id")
    if set(preds) - set(known):
        raise ValueError("prediction references an unknown case")
    if len({r["run_id"] for r in outs}) != 1 or not outs[0]["run_id"].strip():
        raise ValueError("one named inference run required")
    if len({r["model_revision"] for r in outs}) != 1 or not outs[0]["model_revision"].strip():
        raise ValueError("one model revision required")
    test = sorted((r for r in gold if r["split"] == "test"), key=lambda r: r["case_id"])
    if not test or any(r["annotation_status"].lower() != "adjudicated" for r in test):
        raise ValueError("test set needs adjudicated reference labels")
    ids = {r["description_id"] for r in test}
    other_ids = {r["description_id"] for r in gold if r["split"] != "test"}
    if "" in ids or ids & other_ids:
        raise ValueError("cross-split description leakage")
    def predicted(row, key):
        p = preds.get(row["case_id"])
        return (p[key].strip() or ABSTAIN) if p and p["provider_status"].lower() == "ok" else ABSTAIN
    completed = sum(bool(preds.get(r["case_id"])) and
                    preds[r["case_id"]]["provider_status"].lower() == "ok" and
                    all(preds[r["case_id"]][key].strip() for key in
                        ("pred_fault_type", "pred_priority", "pred_risk"))
                    for r in test)
    result = {"evaluation": "offline_after_external_run",
              "run_id": outs[0]["run_id"], "model_revision": outs[0]["model_revision"],
              "test_count": len(test), "complete_count": completed,
              "completion_rate": completed / len(test),
              "source_sha256": {"cases": digest(cases), "predictions": digest(predictions)},
              "approved_for_live_ai": False}
    for tag in ("fault_type", "priority", "risk"):
        result[tag] = metrics([r["gold_" + tag].strip() for r in test],
                              [predicted(r, "pred_" + tag) for r in test])
    return result


def retrieval(gallery, queries, decisions, ranks):
    items = keyed(table(gallery, {"item_id", "group_id", "image_sha256",
                                  "permission_scope", "source_split"}), "item_id")
    query_rows = table(queries, {"query_id", "split", "expected_item_id_or_absent",
                                 "query_image_sha256", "language", "stratum"})
    keyed(query_rows, "query_id")
    all_decisions = table(decisions, {"run_id", "query_id", "decision",
                                      "provider_status", "selected_item_id"})
    dec = keyed(all_decisions, "query_id")
    if len({r["run_id"] for r in all_decisions}) != 1 or not all_decisions[0]["run_id"].strip():
        raise ValueError("single named run required")
    test = [r for r in query_rows if r["split"] == "test"]
    if not test or set(dec) != {q["query_id"] for q in test}:
        raise ValueError("one decision required for every test query")
    candidates = defaultdict(list)
    with Path(ranks).open(newline="", encoding="utf-8-sig") as file:
        reader = csv.DictReader(file)
        required = {"run_id", "query_id", "rank", "candidate_item_id",
                    "permission_allowed", "provider_status"}
        if not required.issubset(reader.fieldnames or []):
            raise ValueError("visible ranked results need required columns")
        for r in reader:
            if r["query_id"] not in dec or r["run_id"] != all_decisions[0]["run_id"]:
                raise ValueError("candidate from unknown query/run")
            if r["candidate_item_id"] not in items:
                raise ValueError("candidate not in gallery")
            if r["permission_allowed"].lower() != "true":
                raise ValueError("unauthorized candidate displayed")
            candidates[r["query_id"]].append(r)
    positives = absent = top1 = top5 = false_accept = correctly_rejected = errors = 0
    for q in test:
        expected = q["expected_item_id_or_absent"].strip()
        if expected != "ABSENT":
            if expected not in items:
                raise ValueError("positive item missing from frozen gallery")
            query_hash = q["query_image_sha256"].strip().lower()
            if query_hash and query_hash == items[expected]["image_sha256"].strip().lower():
                raise ValueError("identical query/gallery photo bytes")
        decision = dec[q["query_id"]]
        action = decision["decision"].strip().lower()
        if action not in {"match", "no_match", "error"}:
            raise ValueError("invalid decision")
        if action != "error" and decision["provider_status"].lower() != "ok":
            raise ValueError("non-error status has provider failure")
        rows = sorted(candidates[q["query_id"]], key=lambda r: int(r["rank"]))
        if [int(r["rank"]) for r in rows] != list(range(1, len(rows) + 1)):
            raise ValueError("non-contiguous visible ranks")
        if len({r["candidate_item_id"] for r in rows}) != len(rows):
            raise ValueError("duplicate candidate")
        if action == "match":
            if not rows or decision["selected_item_id"] != rows[0]["candidate_item_id"]:
                raise ValueError("selected item must be visible rank 1")
        elif rows or decision["selected_item_id"].strip():
            raise ValueError("no-match/error cannot display candidates")
        errors += action == "error"
        if expected == "ABSENT":
            absent += 1
            false_accept += action == "match"
            correctly_rejected += action == "no_match"
        else:
            positives += 1
            rank = next((int(r["rank"]) for r in rows
                         if r["candidate_item_id"] == expected), None)
            top1 += action == "match" and rank == 1
            top5 += action == "match" and rank is not None and rank <= 5
    if positives == 0 or absent == 0:
        raise ValueError("both positive and absent held-out cases are required")
    return {"evaluation": "offline_after_external_run",
            "test_count": len(test), "positive_count": positives, "absent_count": absent,
            "provider_errors_in_denominators": errors,
            "recall_at_1": top1 / positives, "recall_at_5": top5 / positives,
            "absent_false_acceptance_rate": false_accept / absent,
            "absent_correct_rejection_rate": correctly_rejected / absent,
            "input_sha256": {x: digest(y) for x, y in
                             {"gallery": gallery, "queries": queries,
                              "decisions": decisions, "ranks": ranks}.items()},
            "manual_near_duplicate_review_required": True,
            "approved_for_live_search_calibration": False}


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    group = cli.add_subparsers(dest="mode", required=True)
    a = group.add_parser("maintenance")
    a.add_argument("--cases", type=Path, required=True)
    a.add_argument("--predictions", type=Path, required=True)
    b = group.add_parser("retrieval")
    for field in ("gallery", "queries", "decisions", "ranks"):
        b.add_argument("--" + field, type=Path, required=True)
    args = cli.parse_args()
    try:
        result = maintenance(args.cases, args.predictions) if args.mode == "maintenance" \
            else retrieval(args.gallery, args.queries, args.decisions, args.ranks)
    except (ValueError, OSError, csv.Error) as exc:
        cli.error(str(exc))
    print(json.dumps(result, indent=2, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
