import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("restore_verifier", Path(__file__).resolve().parents[1] / "verify-restored-database.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)


class RestoreDefinitionTests(unittest.TestCase):
    def test_equivalent_literal_array_casts_survive_dump_restore(self):
        source = "CHECK (state = ANY ((ARRAY['pending'::character varying, 'approved'::character varying])::text[]))"
        restored = "CHECK (state = ANY (ARRAY[('pending'::character varying)::text, ('approved'::character varying)::text]))"
        self.assertEqual(verifier.canonical_definition(source), verifier.canonical_definition(restored))

    def test_changed_allowed_values_remain_a_restore_failure(self):
        source = "CHECK (state = ANY ((ARRAY['pending'::character varying])::text[]))"
        changed = "CHECK (state = ANY (ARRAY[('deleted'::character varying)::text]))"
        self.assertNotEqual(verifier.canonical_definition(source), verifier.canonical_definition(changed))

    def test_column_expressions_and_bounded_casts_are_not_rewritten(self):
        for expression in ("ARRAY[name::character varying]", "ARRAY['abc'::character varying(2)]", "ARRAY[1::integer]"):
            with self.subTest(expression=expression):
                self.assertEqual(expression, verifier.canonical_definition(expression))

    def test_escaped_quote_literal_preserves_its_value(self):
        source = "(ARRAY['finder''s'::character varying])::text[]"
        self.assertEqual("ARRAY['finder''s'::text]", verifier.canonical_definition(source))
