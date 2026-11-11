"""Offline dataset verifier regression tests: no research payload download needed."""
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from scripts.verify_dataset_manifest import verify


class DatasetManifestTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        (self.root / "stanford").mkdir()
        self.item = self.root / "stanford" / "sample.parquet"
        self.item.write_bytes(b"test-research-data")
        self.manifest = self.root / "manifest.json"
        self.save_manifest()

    def save_manifest(self):
        self.manifest.write_text(json.dumps({"payloads": [{
            "name": "sample.parquet",
            "bytes": len(b"test-research-data"),
            "sha256": hashlib.sha256(b"test-research-data").hexdigest()
        }]}), encoding="utf-8")

    def test_nested_payload_is_verified(self):
        result = verify(self.manifest, self.root)
        self.assertTrue(result["ready_for_offline_review"])
        self.assertEqual(result["verified"], 1)

    def test_missing_is_not_reported_as_pass(self):
        self.item.unlink()
        result = verify(self.manifest, self.root)
        self.assertFalse(result["ready_for_offline_review"])
        self.assertEqual(result["missing"], ["sample.parquet"])

    def test_changed_bytes_fail_integrity(self):
        self.item.write_bytes(b"tEst-research-data")
        result = verify(self.manifest, self.root)
        self.assertFalse(result["ready_for_offline_review"])
        self.assertEqual(result["invalid"][0]["reason"], "sha256 mismatch")

    def test_duplicate_file_is_ambiguous(self):
        (self.root / "sample.parquet").write_bytes(b"test-research-data")
        result = verify(self.manifest, self.root)
        self.assertFalse(result["ready_for_offline_review"])
        self.assertEqual(result["invalid"][0]["reason"], "ambiguous duplicate filename")

    def test_rejects_traversal_and_duplicate_manifest(self):
        payload = json.loads(self.manifest.read_text())["payloads"][0]
        for bad in ([{**payload, "name": "../sample.parquet"}], [payload, payload]):
            self.manifest.write_text(json.dumps({"payloads": bad}), encoding="utf-8")
            with self.assertRaises(ValueError):
                verify(self.manifest, self.root)
