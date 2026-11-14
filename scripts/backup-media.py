"""Copy application media through a configured rclone crypt remote without deleting source objects."""
import argparse
import configparser
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
from urllib.parse import urlparse
import uuid


def validate_configuration(path):
    repository = Path(__file__).resolve().parents[1]
    path = Path(path).resolve()
    if path == repository or repository in path.parents:
        raise ValueError("Private rclone configuration must be outside the repository")
    if os.name != "nt" and stat.S_IMODE(path.stat().st_mode) & 0o077:
        raise ValueError("Private rclone configuration must not be accessible to group/other users")
    config = configparser.ConfigParser(interpolation=None)
    config.read_string(path.read_text(encoding="utf8"))
    source = config["campus-source"]
    endpoint = urlparse(source.get("endpoint", ""))
    if source.get("type") != "s3" or endpoint.scheme != "https" or endpoint.hostname not in (
        "mninpcrhqsaafoshjqpn.supabase.co", "mninpcrhqsaafoshjqpn.storage.supabase.co"
    ) or endpoint.path.rstrip("/") != "/storage/v1/s3" or endpoint.username or endpoint.query or endpoint.fragment:
        raise ValueError("Source must be the approved Supabase project's HTTPS S3 endpoint")
    destination = config["campus-backups"]
    if destination.get("type") != "crypt" or destination.get("filename_encryption", "standard") != "standard" or \
       destination.get("directory_name_encryption", "true").lower() != "true" or not destination.get("password"):
        raise ValueError("Destination must encrypt content, filenames and directory names")
    remote = destination.get("remote", "")
    remote_name, separator, bucket_path = remote.partition(":")
    if not separator or not bucket_path.strip("/") or config[remote_name].get("type") != "oracleobjectstorage":
        raise ValueError("Encrypted destination must use a dedicated OCI Object Storage bucket/path")
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, help="Private rclone INI with campus-source/campus-backups remotes")
    parser.add_argument("--max-transfer", default="50M", help="Per-copy transfer cap; review free egress budget before increasing")
    args = parser.parse_args()
    if not re.fullmatch(r"[1-9][0-9]*[KMG]", args.max_transfer):
        raise ValueError("Use an explicit positive K/M/G transfer cap")
    config = validate_configuration(args.config)
    # Prevent inherited remote overrides or dump/debug flags from defeating the reviewed configuration.
    environment = {key: value for key, value in os.environ.items() if not key.startswith("RCLONE_")}
    common = ["rclone", "--config", str(config), "--log-level", "ERROR", "--retries", "2", "--low-level-retries", "2",
              "--transfers", "1", "--checkers", "2", "--tpslimit", "4"]
    snapshot = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + uuid.uuid4().hex
    for bucket in ("maintenance-images", "lost-found-images"):
        source = "campus-source:" + bucket + "/smart-campus"
        destination = "campus-backups:media/current/" + bucket
        subprocess.run([*common, "copy", source, destination, "--max-transfer", args.max_transfer, "--cutoff-mode", "hard",
                        "--backup-dir", "campus-backups:media/history/" + snapshot + "/" + bucket],
                       env=environment, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True, timeout=1800)
        listing = None
        upload = None
        try:
            listing = subprocess.Popen([*common, "lsjson", source, "--recursive", "--files-only"], env=environment,
                                       stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
            upload = subprocess.Popen([*common, "rcat", "campus-backups:media/manifests/" + snapshot + "/" + bucket + ".json"],
                                      env=environment, stdin=listing.stdout, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            listing.stdout.close()
            if upload.wait(timeout=1800) or listing.wait(timeout=60):
                raise RuntimeError("Encrypted media inventory failed")
        finally:
            for process in (upload, listing):
                if process is not None and process.poll() is None:
                    process.kill()
                    process.wait()
    complete = json.dumps({"snapshot": snapshot, "buckets": ["maintenance-images", "lost-found-images"],
                           "prefix": "smart-campus", "completedUtc": datetime.now(timezone.utc).isoformat()}).encode()
    subprocess.run([*common, "rcat", "campus-backups:media/manifests/" + snapshot + "/complete.json"],
                   input=complete, env=environment, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True, timeout=120)
    print(json.dumps({"result": "media_copy_completed", "snapshot": snapshot,
                      "verification": "Copy and encrypted inventory complete; schedule cryptcheck and an isolated restore rehearsal separately"}))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, configparser.Error, RuntimeError, subprocess.SubprocessError):
        print("Media backup failed; check private configuration, permissions, quotas and provider availability.", file=sys.stderr)
        sys.exit(1)
