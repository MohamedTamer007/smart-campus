"""Stream a PostgreSQL dump into age encryption. Connection secrets use libpq configuration."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
from datetime import datetime, timezone
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--recipient", required=True, help="age public recipient; never its private identity")
    parser.add_argument("--scope", choices=("application", "full"), default="application")
    args = parser.parse_args()
    destination = Path(args.output_dir).resolve()
    repository = Path(__file__).resolve().parents[1]
    if destination == repository or repository in destination.parents:
        raise ValueError("Backups must be outside the repository")
    if not args.recipient.startswith(("age1", "ssh-ed25519 ", "ssh-rsa ")):
        raise ValueError("An age public recipient is required")
    if not os.environ.get("PGDATABASE") and not os.environ.get("PGSERVICE"):
        raise ValueError("Configure the source using PGDATABASE/PGSERVICE and protected libpq credentials")
    if os.name != "nt":
        os.umask(0o077)
    destination.mkdir(parents=True, exist_ok=True)
    name = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex
    partial = destination / (name + ".dump.age.incomplete")
    archive = destination / (name + ".dump.age")
    command = ["pg_dump", "--format=custom", "--no-owner", "--no-acl"]
    if args.scope == "application":
        command.append("--schema=smart_campus")
    dump = None
    encrypt = None
    try:
        with partial.open("xb") as output:
            dump = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
            encrypt = subprocess.Popen(["age", "--encrypt", "--recipient", args.recipient],
                                       stdin=dump.stdout, stdout=output, stderr=subprocess.DEVNULL)
            dump.stdout.close()
            encrypt_code = encrypt.wait(timeout=1800)
            if encrypt_code:
                raise RuntimeError("Backup encryption failed; incomplete archive retained")
            if dump.wait(timeout=60):
                raise RuntimeError("PostgreSQL backup failed; incomplete archive retained")
        partial.rename(archive)
    finally:
        for process in (encrypt, dump):
            if process is not None and process.poll() is None:
                process.kill()
                process.wait()
    with archive.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    manifest = {"format": 1, "scope": args.scope, "createdUtc": datetime.now(timezone.utc).isoformat(),
                "archive": archive.name, "bytes": archive.stat().st_size, "sha256": digest}
    with (destination / (name + ".manifest.json")).open("x", encoding="utf8") as output:
        json.dump(manifest, output, indent=2)
    print(json.dumps({"result": "backup_created", "archive": str(archive), "bytes": manifest["bytes"], "sha256": digest}))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, RuntimeError, subprocess.TimeoutExpired):
        print("Database backup failed; check configuration, tool availability and private operator logs.", file=sys.stderr)
        sys.exit(1)
