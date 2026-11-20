#!/usr/bin/env python3
"""Read-only public SHA/liveness verification; not hosted user acceptance."""
import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import quote, urlsplit
from urllib.request import Request, urlopen

DEFAULT_ORIGIN = "https://smartcampus.tryasp.net"
SHA = re.compile(r"^[a-f0-9]{40}$")


def fetch(origin, path, timeout):
    url = origin.rstrip("/") + path
    request = Request(url, headers={"User-Agent": "SmartCampus-PublicRelease-Audit/1.0",
                                    "Accept": "application/json"})
    with urlopen(request, timeout=timeout) as response:
        final = response.geturl()
        if urlsplit(final).netloc != urlsplit(origin).netloc or urlsplit(final).scheme != "https":
            raise ValueError("unapproved redirect")
        if response.status != 200:
            raise ValueError("HTTP status was not 200")
        data = response.read(32769)
        if len(data) > 32768:
            raise ValueError("response exceeded safety limit")
        return data


def probe(origin, expected, timeout):
    if urlsplit(origin).scheme != "https" or not urlsplit(origin).netloc:
        raise ValueError("public HTTPS origin required")
    if not SHA.fullmatch(expected):
        raise ValueError("expected source SHA must be 40 lowercase hex characters")
    report = {
        "kind": "public_read_only_smoke",
        "observed_utc": datetime.now(timezone.utc).isoformat(),
        "origin": origin,
        "expected_application_commit": expected,
        "health_live": "NOT_RUN",
        "build_marker": "NOT_RUN",
        "authenticated_user_flows": "NOT_EVALUATED",
        "email_inbox_delivery": "NOT_EVALUATED",
        "live_ai_accuracy": "NOT_EVALUATED",
        "hosted_storage_policy": "NOT_EVALUATED",
        "safe_synthetic_only_host": True,
    }
    try:
        fetch(origin, "/health/live", timeout)
        report["health_live"] = "PASS"
        raw = fetch(origin, "/build-info.json?verify=" + quote(expected), timeout)
        marker = json.loads(raw)
        observed = marker.get("commit") if isinstance(marker, dict) else None
        report["observed_application_commit"] = observed if isinstance(observed, str) else None
        report["build_marker"] = "PASS" if observed == expected else "FAIL"
        if report["build_marker"] == "FAIL":
            report["failure_code"] = "SOURCE_SHA_MISMATCH"
    except (OSError, TimeoutError, ValueError, json.JSONDecodeError) as exc:
        # No credentials or response bodies are logged. Network errors remain failures.
        report["failure_code"] = type(exc).__name__
        if report["health_live"] != "PASS":
            report["health_live"] = "FAIL"
        else:
            report["build_marker"] = "FAIL"
    report["result"] = (
        "PASS" if report["health_live"] == "PASS"
        and report["build_marker"] == "PASS" else "FAIL"
    )
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--origin", default=DEFAULT_ORIGIN)
    parser.add_argument("--expected-commit", required=True)
    parser.add_argument("--timeout", type=float, default=15)
    parser.add_argument("--output", type=Path, default=Path("test-results/public-release-check.json"))
    args = parser.parse_args()
    report = probe(args.origin, args.expected_commit, args.timeout)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                           encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["result"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
