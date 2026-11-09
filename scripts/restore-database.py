"""Restore an encrypted application backup only to an empty loopback *_restore_test database."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--identity-file", required=True, help="Private age identity kept outside source control")
    args = parser.parse_args()
    host = os.environ.get("PGHOST", "")
    database = os.environ.get("PGDATABASE", "")
    if host not in ("127.0.0.1", "localhost") or not database.endswith("_restore_test"):
        raise ValueError("Restore requires explicit loopback PGHOST and a database ending _restore_test")
    environment = os.environ.copy()
    environment.pop("PGSERVICE", None)
    environment.pop("PGHOSTADDR", None)
    manifest_path = Path(args.manifest).resolve()
    document = json.loads(manifest_path.read_text(encoding="utf8"))
    filename = document["archive"]
    if document["format"] != 1 or document["scope"] != "application" or not isinstance(filename, str) or Path(filename).name != filename:
        raise ValueError("Only an application backup with a local archive filename may be restored")
    archive = manifest_path.parent / filename
    with archive.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    if digest != document["sha256"] or archive.stat().st_size != document["bytes"]:
        raise ValueError("Archive integrity verification failed")
    connection = ["--host", host, "--dbname", database]
    check = subprocess.run(["psql", *connection, "--tuples-only", "--no-align", "--set", "ON_ERROR_STOP=1", "--command",
                            "SELECT count(*) FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema');"],
                           env=environment, capture_output=True, text=True, timeout=30, check=True)
    if check.stdout.strip() != "0":
        raise ValueError("Restore target is not empty")
    decrypt = None
    restore = None
    try:
        decrypt = subprocess.Popen(["age", "--decrypt", "--identity", args.identity_file, str(archive)],
                                   stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        restore = subprocess.Popen(["pg_restore", *connection, "--exit-on-error", "--no-owner", "--no-acl"],
                                   stdin=decrypt.stdout, stderr=subprocess.DEVNULL, stdout=subprocess.DEVNULL, env=environment)
        decrypt.stdout.close()
        if restore.wait(timeout=1800):
            raise RuntimeError("Restore failed; isolated target may be partial and must be inspected")
        if decrypt.wait(timeout=60):
            raise RuntimeError("Backup decryption failed")
    finally:
        for process in (restore, decrypt):
            if process is not None and process.poll() is None:
                process.kill()
                process.wait()
    print(json.dumps({"result": "isolated_restore_completed", "database": database, "archiveSha256": digest}))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError):
        print("Isolated restore failed; check archive, identity, empty target and required PostgreSQL extensions.", file=sys.stderr)
        sys.exit(1)
