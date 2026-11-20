"""Offline contract tests for the non-certifying DOI metadata screening."""
import importlib.util
from pathlib import Path
from urllib.error import URLError
import unittest

script = Path(__file__).resolve().parents[1] / "audit_reference_metadata.py"
spec = importlib.util.spec_from_file_location("reference_metadata", script)
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)

EXAMPLE = {
    "number": 2,
    "first_author": "Abdul-Mageed",
    "year": 2014,
    "title": "SAMAR: Subjectivity and sentiment analysis for Arabic social media",
    "doi": "10.1016/j.csl.2013.03.001",
    "source_url": "https://doi.org/10.1016/j.csl.2013.03.001",
}
METADATA = {
    "title": [EXAMPLE["title"]],
    "author": [{"family": EXAMPLE["first_author"]}],
    "published-print": {"date-parts": [[2014, 1]]},
    "published-online": {"date-parts": [[2013, 3]]},
    "container-title": ["Computer Speech & Language"],
}


class CrossrefMetadataTests(unittest.TestCase):
    def test_updated_reference_register_has_91_entries_and_72_resolvable_dois(self):
        root = Path(__file__).resolve().parents[2]
        path = root / "docs" / "REFERENCE_AUDIT_2026-11-20.md"
        rows = audit.parse_register(path.read_text(encoding="utf-8"))
        self.assertEqual(len(rows), 91)
        self.assertEqual(sum(bool(x["doi"]) for x in rows), 72)
        # #83 now prints the publisher DOI verified against the Monash record.
        self.assertEqual(rows[82]["doi"], "10.1007/s10664-014-9331-y")
        self.assertEqual(rows[0]["number"], 1)
        self.assertEqual(rows[-1]["number"], 91)

    def test_exact_publisher_metadata_and_online_year_variant(self):
        outcome = audit.compare_entry(EXAMPLE, METADATA)
        self.assertEqual(outcome["status"], "LIKELY_ALIGNED")
        self.assertEqual(outcome["crossref_publication_years"], [2013, 2014])
        self.assertTrue(outcome["first_author_matches"])

    def test_doi_does_not_override_wrong_title_or_first_author(self):
        altered = dict(METADATA, title=["A paper about an unrelated experiment"])
        self.assertEqual(audit.compare_entry(EXAMPLE, altered)["status"], "REVIEW_METADATA")
        altered = dict(METADATA, author=[{"family": "Different"}])
        self.assertFalse(audit.compare_entry(EXAMPLE, altered)["first_author_matches"])

    def test_unavailable_provider_and_non_doi_are_not_misrepresented(self):
        other = dict(EXAMPLE, number=1, doi=None, source_url="https://aclanthology.org/")
        def offline(doi, timeout):
            raise URLError("synthetic connection failure")
        results = audit.audit([EXAMPLE, other], delay=0, lookup=offline)
        self.assertEqual(results[0]["status"], "CROSSREF_NOT_VERIFIED")
        self.assertEqual(results[1]["status"], "NON_DOI_SOURCE_REVIEW")

    def test_duplicate_or_missing_register_rows_fail_closed(self):
        sample = "| 1 | X, A. (2020). Research title. https://doi.org/10.1000/abc | [Source](https://doi.org/10.1000/abc) |"
        self.assertEqual(len(audit.parse_register(sample)), 1)
        with self.assertRaises(ValueError):
            audit.parse_register(sample + "\n" + sample)


if __name__ == "__main__":
    unittest.main()
