#!/usr/bin/env python3
"""Verify downloaded Smart Campus research datasets against the Drive manifest.

This tool is offline and read-only. It never uploads images, modifies the
production database or reclassifies example data as genuine campus records.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


def verify(manifest_path: Path, data_dir: Path) -> dict:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    payloads = manifest.get("payloads")
    if not isinstance(payloads, list) or not payloads:
        raise ValueError("manifest must contain a nonempty payloads array")

    # The Drive collection stores ZIPs and Parquet parts in separate folders.
    locations: dict[str, list[Path]] = {}
    for path in data_dir.rglob("*"):
        if path.is_file():
            locations.setdefault(path.name, []).append(path)

    seen: set[str] = set()
    checked, missing, invalid = [], [], []
    for item in payloads:
        if not isinstance(item, dict):
            raise ValueError("malformed payload")
        name, size, expected = item.get("name"), item.get("bytes"), item.get("sha256")
        if (
            not isinstance(name, str)
            or not name
            or Path(name).name != name
            or name in seen
            or not isinstance(size, int)
            or size < 0
            or not isinstance(expected, str)
            or len(expected) != 64
            or any(c not in "0123456789abcdefABCDEF" for c in expected)
        ):
            raise ValueError("invalid/duplicate manifest filename, length or digest")
        seen.add(name)
        candidates = locations.get(name, [])
        if not candidates:
            missing.append(name)
            continue
        if len(candidates) != 1:
            invalid.append({"file": name, "reason": "ambiguous duplicate filename"})
            continue
        path = candidates[0]
        if path.stat().st_size != size:
            invalid.append({"file": name, "reason": "size mismatch"})
            continue
        digest = hashlib.sha256()
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
        if digest.hexdigest().lower() != expected.lower():
            invalid.append({"file": name, "reason": "sha256 mismatch"})
            continue
        checked.append(name)
    return {
        "expected": len(payloads),
        "verified": len(checked),
        "missing": missing,
        "invalid": invalid,
        "ready_for_offline_review": len(checked) == len(payloads) and not invalid,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--data-dir", type=Path, required=True)
    parser.add_argument("--json-output", type=Path, help="optional local report filename")
    args = parser.parse_args()
    if not args.manifest.is_file() or not args.data_dir.is_dir():
        parser.error("provide an existing manifest and data directory")
    try:
        result = verify(args.manifest, args.data_dir)
    except (ValueError, OSError, json.JSONDecodeError) as exc:
        parser.error(str(exc))
    report = json.dumps(result, ensure_ascii=False, indent=2)
    print(report)
    if args.json_output:
        args.json_output.write_text(report + "\n", encoding="utf-8")
    return 0 if result["ready_for_offline_review"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
