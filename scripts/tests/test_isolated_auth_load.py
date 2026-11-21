"""Offline safety/metric contracts for G7 loopback-only synthetic load."""
import importlib.util
from pathlib import Path
import unittest

target = Path(__file__).resolve().parents[1] / "measure_isolated_auth_load.py"
spec = importlib.util.spec_from_file_location("isolated_load", target)
load = importlib.util.module_from_spec(spec)
spec.loader.exec_module(load)


class IsolatedLoadContractTests(unittest.TestCase):
    def test_percentiles_use_nearest_rank_and_never_interpolate_away_outliers(self):
        values = list(range(1, 101))
        self.assertEqual(load.percentile(values, .50), 50)
        self.assertEqual(load.percentile(values, .95), 95)
        self.assertEqual(load.percentile(values, .99), 99)
        self.assertEqual(load.percentile([90], .99), 90)
        with self.assertRaises(ValueError):
            load.percentile([], .95)

    def test_remote_host_https_ipv6_credentials_and_other_ports_are_refused(self):
        for url in [
            "https://smartcampus.tryasp.net",
            "http://smartcampus.tryasp.net:5080",
            "https://127.0.0.1:5080",
            "http://127.0.0.1:80",
            "http://127.0.0.1:5080/api",
            "http://localhost:5080/?q=1",
            "http://name:pass@127.0.0.1:5080",
            "http://[::1]:5080",
        ]:
            with self.subTest(url=url), self.assertRaises(ValueError):
                load.require_loopback(url)

    def test_explicit_approved_loopback_is_accepted(self):
        self.assertEqual(load.require_loopback("http://127.0.0.1:5080/"), "http://127.0.0.1:5080")
        self.assertEqual(load.require_loopback("http://localhost:5080"), "http://localhost:5080")


if __name__ == "__main__":
    unittest.main()
