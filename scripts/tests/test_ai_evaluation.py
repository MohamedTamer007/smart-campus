"""Only synthetic scorer contracts; do not misreport these fixtures as AI quality."""
import csv
import importlib.util
from pathlib import Path
import tempfile
import unittest

file = Path(__file__).resolve().parents[1] / "score_ai_evaluation.py"
spec = importlib.util.spec_from_file_location("ai_evaluation_scorer", file)
scorer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scorer)


class AiEvidenceTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.folder = Path(temp.name)

    def write(self, name, rows):
        path = self.folder / name
        with path.open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, list(rows[0].keys()))
            writer.writeheader()
            writer.writerows(rows)
        return path

    def maintenance(self, predictions=None, cases=None):
        cases = cases or [
            {"case_id": "a", "split": "test", "description_id": "d1", "annotation_status": "adjudicated",
             "gold_fault_type": "electrical", "gold_priority": "high", "gold_risk": "high"},
            {"case_id": "b", "split": "test", "description_id": "d2", "annotation_status": "adjudicated",
             "gold_fault_type": "plumbing", "gold_priority": "low", "gold_risk": "low"}]
        predictions = predictions if predictions is not None else [
            {"run_id": "synthetic", "case_id": c["case_id"], "model_revision": "fixture",
             "pred_fault_type": c["gold_fault_type"], "pred_priority": c["gold_priority"],
             "pred_risk": c["gold_risk"], "provider_status": "ok"} for c in cases]
        return scorer.maintenance(self.write("cases.csv", cases), self.write("pred.csv", predictions))

    def retrieval(self, queries=None, decisions=None, ranked=None):
        gallery = [{"item_id": "a", "group_id": "g1", "image_sha256": "aaa",
                    "permission_scope": "fictional", "source_split": "gallery"},
                   {"item_id": "b", "group_id": "g2", "image_sha256": "bbb",
                    "permission_scope": "fictional", "source_split": "gallery"}]
        queries = queries if queries is not None else [
            {"query_id": "q1", "split": "test", "expected_item_id_or_absent": "a",
             "query_image_sha256": "different", "language": "ar", "stratum": "distinct"},
            {"query_id": "q2", "split": "test", "expected_item_id_or_absent": "ABSENT",
             "query_image_sha256": "", "language": "ar", "stratum": "absent"}]
        decisions = decisions if decisions is not None else [
            {"run_id": "synthetic", "query_id": "q1", "decision": "match",
             "provider_status": "ok", "selected_item_id": "a"},
            {"run_id": "synthetic", "query_id": "q2", "decision": "no_match",
             "provider_status": "ok", "selected_item_id": ""}]
        ranked = ranked if ranked is not None else [
            {"run_id": "synthetic", "query_id": "q1", "rank": "1",
             "candidate_item_id": "a", "permission_allowed": "true",
             "provider_status": "ok"}]
        return scorer.retrieval(self.write("gallery.csv", gallery),
                                self.write("queries.csv", queries),
                                self.write("decisions.csv", decisions),
                                self.write("ranks.csv", ranked))

    def test_maintenance_correct_synthetic_fixture(self):
        r = self.maintenance()
        self.assertEqual(r["fault_type"]["macro_f1"], 1)
        self.assertFalse(r["approved_for_live_ai"])

    def test_provider_failure_reduces_maintenance_score(self):
        p = [{"run_id": "x", "case_id": "a", "model_revision": "m",
              "pred_fault_type": "electrical", "pred_priority": "high",
              "pred_risk": "high", "provider_status": "ok"},
             {"run_id": "x", "case_id": "b", "model_revision": "m",
              "pred_fault_type": "plumbing", "pred_priority": "low",
              "pred_risk": "low", "provider_status": "timeout"}]
        r = self.maintenance(predictions=p)
        self.assertEqual(r["complete_count"], 1)
        self.assertEqual(r["risk"]["accuracy_including_failures"], .5)

    def test_explicit_abstention_is_not_counted_as_completed(self):
        predictions = [
            {"run_id": "synthetic", "case_id": "a", "model_revision": "fixture",
             "pred_fault_type": "electrical", "pred_priority": "high",
             "pred_risk": "__ABSTAIN__", "provider_status": "ok"},
            {"run_id": "synthetic", "case_id": "b", "model_revision": "fixture",
             "pred_fault_type": "plumbing", "pred_priority": "low",
             "pred_risk": "low", "provider_status": "ok"}]
        result = self.maintenance(predictions=predictions)
        self.assertEqual(result["complete_count"], 1)
        self.assertEqual(result["risk"]["accuracy_including_failures"], .5)

    def test_unknown_predictions_rejected(self):
        p = [{"run_id": "x", "case_id": "unknown", "model_revision": "m",
              "pred_fault_type": "x", "pred_priority": "y",
              "pred_risk": "z", "provider_status": "ok"}]
        with self.assertRaisesRegex(ValueError, "unknown"):
            self.maintenance(predictions=p)

    def test_unreviewed_gold_rejected(self):
        cases = [{"case_id": "a", "split": "test", "description_id": "d",
                  "annotation_status": "draft", "gold_fault_type": "f",
                  "gold_priority": "p", "gold_risk": "r"}]
        with self.assertRaisesRegex(ValueError, "adjudicated"):
            self.maintenance(cases=cases)

    def test_retrieval_correct_synthetic_fixture(self):
        r = self.retrieval()
        self.assertEqual(r["recall_at_1"], 1)
        self.assertEqual(r["absent_correct_rejection_rate"], 1)
        self.assertFalse(r["approved_for_live_search_calibration"])

    def test_same_photo_rejected(self):
        queries = [{"query_id": "q1", "split": "test", "expected_item_id_or_absent": "a",
                    "query_image_sha256": "aaa", "language": "ar", "stratum": "same"},
                   {"query_id": "q2", "split": "test", "expected_item_id_or_absent": "ABSENT",
                    "query_image_sha256": "", "language": "ar", "stratum": "absent"}]
        with self.assertRaisesRegex(ValueError, "identical"):
            self.retrieval(queries=queries)

    def test_same_photo_as_wrong_gallery_item_rejected(self):
        queries = [
            {"query_id": "q1", "split": "test", "expected_item_id_or_absent": "a",
             "query_image_sha256": "bbb", "language": "ar", "stratum": "image"},
            {"query_id": "q2", "split": "test", "expected_item_id_or_absent": "ABSENT",
             "query_image_sha256": "", "language": "ar", "stratum": "absent"}]
        with self.assertRaisesRegex(ValueError, "identical"):
            self.retrieval(queries=queries)

    def test_failed_provider_rank_is_rejected(self):
        ranked = [{"run_id": "synthetic", "query_id": "q1", "rank": "1",
                   "candidate_item_id": "a", "permission_allowed": "true",
                   "provider_status": "failed"}]
        with self.assertRaisesRegex(ValueError, "failed provider"):
            self.retrieval(ranked=ranked)

    def test_repeated_heldout_photo_rejected(self):
        queries = [
            {"query_id": "q1", "split": "test", "expected_item_id_or_absent": "a",
             "query_image_sha256": "new-image", "language": "ar", "stratum": "image"},
            {"query_id": "q2", "split": "test", "expected_item_id_or_absent": "ABSENT",
             "query_image_sha256": "new-image", "language": "ar", "stratum": "absent"}]
        with self.assertRaisesRegex(ValueError, "repeated"):
            self.retrieval(queries=queries)

    def test_cross_split_photo_leakage_rejected(self):
        queries = [
            {"query_id": "q1", "split": "test", "expected_item_id_or_absent": "a",
             "query_image_sha256": "fresh-photo", "language": "ar", "stratum": "image"},
            {"query_id": "q2", "split": "test", "expected_item_id_or_absent": "ABSENT",
             "query_image_sha256": "", "language": "ar", "stratum": "absent"},
            {"query_id": "q3", "split": "validation", "expected_item_id_or_absent": "a",
             "query_image_sha256": "fresh-photo", "language": "ar", "stratum": "image"}]
        with self.assertRaisesRegex(ValueError, "cross-split"):
            self.retrieval(queries=queries)

    def test_missing_retrieval_decision_rejected(self):
        p = [{"run_id": "synthetic", "query_id": "q1", "decision": "match",
              "provider_status": "ok", "selected_item_id": "a"}]
        with self.assertRaisesRegex(ValueError, "every test"):
            self.retrieval(decisions=p)

    def test_unauthorized_candidate_rejected(self):
        r = [{"run_id": "synthetic", "query_id": "q1", "rank": "1",
              "candidate_item_id": "a", "permission_allowed": "false",
              "provider_status": "ok"}]
        with self.assertRaisesRegex(ValueError, "unauthorized"):
            self.retrieval(ranked=r)

    def test_absent_false_acceptance_is_detected(self):
        p = [{"run_id": "synthetic", "query_id": "q1", "decision": "match",
              "provider_status": "ok", "selected_item_id": "a"},
             {"run_id": "synthetic", "query_id": "q2", "decision": "match",
              "provider_status": "ok", "selected_item_id": "b"}]
        r = [{"run_id": "synthetic", "query_id": "q1", "rank": "1",
              "candidate_item_id": "a", "permission_allowed": "true", "provider_status": "ok"},
             {"run_id": "synthetic", "query_id": "q2", "rank": "1",
              "candidate_item_id": "b", "permission_allowed": "true", "provider_status": "ok"}]
        self.assertEqual(self.retrieval(decisions=p, ranked=r)["absent_false_acceptance_rate"], 1)

    def test_errors_do_not_count_as_correct_no_match(self):
        p = [{"run_id": "synthetic", "query_id": "q1", "decision": "match",
              "provider_status": "ok", "selected_item_id": "a"},
             {"run_id": "synthetic", "query_id": "q2", "decision": "error",
              "provider_status": "failed", "selected_item_id": ""}]
        self.assertEqual(self.retrieval(decisions=p)["absent_correct_rejection_rate"], 0)

if __name__ == "__main__":
    unittest.main()
