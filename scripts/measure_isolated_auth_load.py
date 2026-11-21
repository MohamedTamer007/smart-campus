#!/usr/bin/env python3
"""G7 reproducible signed-in API load on loopback-only synthetic CI; not hosted acceptance.

Ten independent HTTP cookie sessions sign in as the *same* synthetic seeded student.
The runner performs only read-only ordinary API requests, never logs bodies, usernames,
cookies or passwords. This establishes a scoped runner measurement, not a university
capacity, multi-identity or real-host guarantee.
"""
import argparse
import json
import math
import os
import time
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from http.cookiejar import CookieJar
from pathlib import Path
from threading import Barrier
from urllib.error import HTTPError, URLError
from urllib.parse import urlsplit
from urllib.request import HTTPCookieProcessor, HTTPRedirectHandler, Request, build_opener

ROUTES = (
    "/api/v1/auth/me",
    "/api/v1/maintenance?page=1&pageSize=1",
    "/api/v1/directory/locations",
)


class RejectRedirects(HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        return None


def require_loopback(origin):
    url = urlsplit(origin)
    if url.scheme != "http" or url.hostname not in {"localhost", "127.0.0.1"} or url.username or url.password or url.path not in {"", "/"} or url.query or url.fragment:
        raise ValueError("isolated API load accepts only http://localhost or 127.0.0.1, without paths or credentials")
    if url.port not in {5080}:
        raise ValueError("isolated API load accepts only the approved CI port 5080")
    return origin.rstrip("/")


def percentile(values, proportion):
    if not values:
        raise ValueError("empty latency sample")
    return sorted(values)[max(0, math.ceil(len(values) * proportion) - 1)]


def open_request(opener, request, timeout=8):
    with opener.open(request, timeout=timeout) as response:
        if response.status != 200:
            raise ValueError("unexpected HTTP status")
        # Bound reads and discard all response bodies rather than leaking synthetic records.
        if len(response.read(65537)) > 65536:
            raise ValueError("oversized test response")


def signed_in_opener(origin, email, password):
    opener = build_opener(HTTPCookieProcessor(CookieJar()), RejectRedirects)
    with opener.open(Request(origin + "/api/v1/auth/csrf", headers={"Accept": "application/json"}), timeout=8) as response:
        if response.status != 200:
            raise ValueError("csrf endpoint denied synthetic session")
        csrf = json.loads(response.read(8192))["token"]
    request = Request(origin + "/api/v1/auth/login",
                      data=json.dumps({"email": email, "password": password}).encode("utf-8"),
                      headers={"Content-Type": "application/json", "X-CSRF-TOKEN": csrf},
                      method="POST")
    open_request(opener, request)
    # Verify the cookie is usable before collecting timings.
    open_request(opener, Request(origin + "/api/v1/auth/me"))
    return opener


def measure(origin, fixture, sessions, requests_per_session):
    with Path(fixture).open(encoding="utf-8-sig") as source:
        data = json.load(source)
    account = data["accounts"]["student"]
    email = account["email"]
    password = data["password"]
    openers = [signed_in_opener(origin, email, password) for _ in range(sessions)]
    # Release sensitive fixture values from local variables after login.
    del email, password, data, account
    barrier = Barrier(sessions)

    def client(index):
        measurements = []
        failures = []
        barrier.wait(timeout=30)
        for attempt in range(requests_per_session):
            path = ROUTES[(index + attempt) % len(ROUTES)]
            start = time.perf_counter()
            try:
                open_request(openers[index], Request(origin + path, headers={"Accept": "application/json", "Cache-Control": "no-store"}))
                measurements.append((time.perf_counter() - start) * 1000)
            except (HTTPError, URLError, ValueError, TimeoutError, OSError) as exc:
                failures.append(type(exc).__name__)
        return measurements, failures

    started = time.perf_counter()
    with ThreadPoolExecutor(max_workers=sessions) as pool:
        results = list(pool.map(client, range(sessions)))
    seconds = time.perf_counter() - started
    latencies = [value for samples, _ in results for value in samples]
    errors = [value for _, failures in results for value in failures]
    return {
        "kind": "isolated_signed_in_read_only_load",
        "observed_utc": datetime.now(timezone.utc).isoformat(),
        "source_sha": os.getenv("GITHUB_SHA", "local"),
        "environment": "isolated CI loopback / PostgreSQL / synthetic demo fixture",
        "identity_scope": "one fictional seeded student, ten distinct authenticated cookie sessions",
        "hosted_result": "NOT_EVALUATED",
        "approved_student_data": "NOT_USED",
        "screen_reader_acceptance": "NOT_EVALUATED",
        "concurrent_sessions": sessions,
        "configured_requests_per_session": requests_per_session,
        "attempted_requests": sessions * requests_per_session,
        "successful_requests": len(latencies),
        "errors": len(errors),
        "error_types": sorted(set(errors)),
        "p50_ms": round(percentile(latencies, 0.50), 2) if latencies else None,
        "p95_ms": round(percentile(latencies, 0.95), 2) if latencies else None,
        "p99_ms": round(percentile(latencies, 0.99), 2) if latencies else None,
        "throughput_per_second": round(len(latencies) / seconds, 2) if seconds else 0,
        "duration_seconds": round(seconds, 2),
    }


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("--origin", default="http://127.0.0.1:5080")
    cli.add_argument("--fixture", default="backend/SmartCampus.Api/local-data/demo-accounts.json")
    cli.add_argument("--sessions", type=int, default=10)
    cli.add_argument("--requests-per-session", type=int, default=20)
    cli.add_argument("--p95-limit-ms", type=float, default=2000)
    cli.add_argument("--output", required=True)
    args = cli.parse_args()
    origin = require_loopback(args.origin)
    if args.sessions != 10 or not 1 <= args.requests_per_session <= 40 or not 0 < args.p95_limit_ms <= 2000:
        raise SystemExit("Load profile must use exactly 10 sessions, 1..40 requests/session and approved p95 target <=2000ms.")
    report = measure(origin, args.fixture, args.sessions, args.requests_per_session)
    report["p95_target_ms"] = args.p95_limit_ms
    report["result"] = "PASS" if report["errors"] == 0 and report["successful_requests"] == report["attempted_requests"] and report["p95_ms"] is not None and report["p95_ms"] < args.p95_limit_ms else "FAIL"
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, sort_keys=True))
    if report["result"] != "PASS":
        raise SystemExit("Isolated API load failed the declared target; see sanitized artifact.")


if __name__ == "__main__":
    main()
