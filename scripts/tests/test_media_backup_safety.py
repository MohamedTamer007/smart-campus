import importlib.util
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import uuid


spec = importlib.util.spec_from_file_location("backup_media", Path(__file__).resolve().parents[1] / "backup-media.py")
media = importlib.util.module_from_spec(spec)
spec.loader.exec_module(media)


class MediaBackupSafetyTests(unittest.TestCase):
    def configuration(self, change=""):
        directory = Path(tempfile.gettempdir()) / ("campus-media-safety-" + uuid.uuid4().hex)
        directory.mkdir()
        path = directory / "test-rclone.conf"
        content = """[campus-source]
type = s3
endpoint = https://mninpcrhqsaafoshjqpn.storage.supabase.co/storage/v1/s3
[campus-backups]
type = crypt
remote = private-oci:test-backups/application
password = synthetic-obscured-test-password
filename_encryption = standard
directory_name_encryption = true
[private-oci]
type = oracleobjectstorage
"""
        if change:
            before, after = change.split("=>", 1)
            content = content.replace(before, after)
        path.write_text(content, encoding="utf8")
        path.chmod(0o600)
        return path

    def test_plaintext_or_wrong_project_backup_is_rejected_before_tool_execution(self):
        changes = (
            "type = crypt=>type = local",
            "filename_encryption = standard=>filename_encryption = off",
            "directory_name_encryption = true=>directory_name_encryption = false",
            "mninpcrhqsaafoshjqpn.storage.supabase.co=>other-project.storage.supabase.co",
            "https://=>http://",
            "type = oracleobjectstorage=>type = local",
        )
        for change in changes:
            with self.subTest(change=change), patch("sys.argv", ["backup-media", "--config", str(self.configuration(change))]), \
                 patch.object(media.subprocess, "run") as copy:
                with self.assertRaises(ValueError):
                    media.main()
                copy.assert_not_called()

    def test_invalid_transfer_cap_is_rejected_before_external_access(self):
        with patch("sys.argv", ["backup-media", "--config", "not-opened", "--max-transfer", "unlimited"]), \
             patch.object(media.subprocess, "run") as copy:
            with self.assertRaises(ValueError):
                media.main()
            copy.assert_not_called()

    @unittest.skipIf(os.name == "nt", "POSIX file permissions are verified on the deployment platform")
    def test_world_readable_backup_credentials_are_rejected(self):
        path = self.configuration()
        path.chmod(0o644)
        with self.assertRaisesRegex(ValueError, "group/other"):
            media.validate_configuration(path)


if __name__ == "__main__":
    unittest.main()
