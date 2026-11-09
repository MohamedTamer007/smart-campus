"""Safety boundaries only; real encryption/restore is exercised separately in CI."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import uuid


SCRIPTS = Path(__file__).resolve().parents[1]


def load(name):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


backup = load("backup-database")
restore = load("restore-database")


class BackupSafetyTests(unittest.TestCase):
    def test_backup_refuses_repository_destination_before_running_tools(self):
        arguments = ["backup", "--output-dir", str(SCRIPTS.parent), "--recipient", "age1synthetic-test-recipient"]
        with patch("sys.argv", arguments), patch.object(backup.subprocess, "Popen") as process:
            with self.assertRaises(ValueError):
                backup.main()
            process.assert_not_called()

    def test_backup_requires_explicit_source_before_creating_directory(self):
        directory = Path(tempfile.gettempdir()) / ("campus-no-backup-" + uuid.uuid4().hex)
        arguments = ["backup", "--output-dir", str(directory), "--recipient", "age1synthetic-test-recipient"]
        with patch("sys.argv", arguments), patch.dict(os.environ, {}, clear=True), patch.object(backup.subprocess, "Popen") as process:
            with self.assertRaises(ValueError):
                backup.main()
            self.assertFalse(directory.exists())
            process.assert_not_called()

    def test_restore_rejects_remote_or_non_test_targets_before_opening_archive(self):
        for host, database in (("database.example.test", "campus_restore_test"), ("127.0.0.1", "campus"), ("", "campus_restore_test")):
            with self.subTest(host=host, database=database):
                arguments = ["restore", "--manifest", "not-opened.json", "--identity-file", "not-opened.key"]
                with patch("sys.argv", arguments), patch.dict(os.environ, {"PGHOST": host, "PGDATABASE": database}, clear=True), \
                     patch.object(restore.subprocess, "run") as query, patch.object(restore.subprocess, "Popen") as process:
                    with self.assertRaises(ValueError):
                        restore.main()
                    query.assert_not_called()
                    process.assert_not_called()

    def archive(self, **changes):
        directory = Path(tempfile.gettempdir()) / ("campus-backup-safety-" + uuid.uuid4().hex)
        directory.mkdir()
        # Synthetic bytes intentionally do not pretend to be an encrypted database.
        payload = b"synthetic-integrity-fixture"
        (directory / "fixture.dump.age").write_bytes(payload)
        manifest = {"format": 1, "scope": "application", "archive": "fixture.dump.age",
                    "bytes": len(payload), "sha256": hashlib.sha256(payload).hexdigest()}
        manifest.update(changes)
        path = directory / "fixture.manifest.json"
        path.write_text(json.dumps(manifest), encoding="utf8")
        return ["restore", "--manifest", str(path), "--identity-file", "not-used.key"]

    def test_restore_rejects_tampered_or_traversing_archive_before_database_access(self):
        for changes in ({"sha256": "0" * 64}, {"bytes": 999}, {"archive": "../escape.dump.age"}, {"archive": None}, {"scope": "full"}):
            with self.subTest(changes=changes):
                with patch("sys.argv", self.archive(**changes)), \
                     patch.dict(os.environ, {"PGHOST": "127.0.0.1", "PGDATABASE": "campus_restore_test"}, clear=True), \
                     patch.object(restore.subprocess, "run") as query, patch.object(restore.subprocess, "Popen") as process:
                    with self.assertRaises(ValueError):
                        restore.main()
                    query.assert_not_called()
                    process.assert_not_called()

    def test_restore_does_not_modify_nonempty_target(self):
        query_result = subprocess.CompletedProcess([], 0, stdout="1\n", stderr="")
        with patch("sys.argv", self.archive()), \
             patch.dict(os.environ, {"PGHOST": "127.0.0.1", "PGDATABASE": "campus_restore_test"}, clear=True), \
             patch.object(restore.subprocess, "run", return_value=query_result), patch.object(restore.subprocess, "Popen") as process:
            with self.assertRaisesRegex(ValueError, "not empty"):
                restore.main()
            process.assert_not_called()


if __name__ == "__main__":
    unittest.main()
