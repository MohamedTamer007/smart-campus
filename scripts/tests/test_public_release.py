"""Synthetic, offline public-probe tests; do not claim hosted acceptance."""
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

target = Path(__file__).resolve().parents[1] / "verify_public_release.py"
spec = importlib.util.spec_from_file_location("public_release_probe", target)
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)

COMMIT = "eb3c62c2c743ce792a53f311ce97c3751d422437"


class FakeResponse:
    status = 200

    def __init__(self, url, data):
        self.url = url
        self.data = data

    def geturl(self):
        return self.url

    def read(self, count=-1):
        return self.data[:count]

    def __enter__(self):
        return self

    def __exit__(self, *args):
        return False


def good_urlopen(request, timeout):
    url = request.full_url
    if "/health/live" in url:
        return FakeResponse(url, b"Healthy")
    if "/build-info.json" in url:
        return FakeResponse(url, json.dumps({"commit": COMMIT}).encode())
    raise AssertionError("unexpected endpoint")


class PublicReleaseProbeTests(unittest.TestCase):
    @patch.object(probe, "urlopen", good_urlopen)
    def test_synthetic_baseline_passes(self):
        r = probe.probe(probe.DEFAULT_ORIGIN, COMMIT, 5)
        self.assertEqual(r["result"], "PASS")
        self.assertEqual(r["live_ai_accuracy"], "NOT_EVALUATED")
        self.assertEqual(r["email_inbox_delivery"], "NOT_EVALUATED")

    @patch.object(probe, "urlopen")
    def test_mismatched_commit_fails(self, mock):
        mock.side_effect = lambda req, timeout: FakeResponse(
            req.full_url,
            b"Healthy" if "/health/live" in req.full_url else
            b'{"commit":"0000000000000000000000000000000000000000"}'
        )
        r = probe.probe(probe.DEFAULT_ORIGIN, COMMIT, 5)
        self.assertEqual(r["result"], "FAIL")
        self.assertEqual(r["failure_code"], "SOURCE_SHA_MISMATCH")

    @patch.object(probe, "urlopen")
    def test_network_error_fails_closed(self, mock):
        mock.side_effect = OSError("synthetic failure")
        r = probe.probe(probe.DEFAULT_ORIGIN, COMMIT, 5)
        self.assertEqual(r["health_live"], "FAIL")
        self.assertEqual(r["result"], "FAIL")

    def test_invalid_expected_commit_rejected(self):
        with self.assertRaisesRegex(ValueError, "40 lowercase hex"):
            probe.probe(probe.DEFAULT_ORIGIN, "main", 5)

    @patch.object(probe, "urlopen")
    def test_redirect_to_unapproved_host_rejected(self, mock):
        mock.return_value = FakeResponse("https://other.example/health/live", b"Healthy")
        r = probe.probe(probe.DEFAULT_ORIGIN, COMMIT, 5)
        self.assertEqual(r["result"], "FAIL")


if __name__ == "__main__":
    unittest.main()
