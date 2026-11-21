import importlib.util
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "skills" / "mortis-architect" / "scripts"


def load(name):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


decisions = load("decisions")
new_decisions = load("new_decisions")


def make(preset="standard", language="csharp", database="postgres", **overrides):
    return new_decisions.build("Acme", language, database, preset, {key.replace("__", "."): value for key, value in overrides.items()})


def run(text):
    errors, notes, values = decisions.validate_text(text)
    return errors, notes, values


def rules_in(errors):
    return {error["rule"] for error in errors if error["rule"]}


class PresetTests(unittest.TestCase):
    def test_lean_is_valid(self):
        errors, notes, values = run(make("lean"))
        self.assertEqual(errors, [])
        self.assertEqual(values["events.outbox"], "none")

    def test_standard_is_valid(self):
        errors, notes, values = run(make("standard"))
        self.assertEqual(errors, [])
        self.assertEqual(values["events.transport"], "database")
        self.assertEqual(values["addons"], ["correlation", "api-conventions"])

    def test_advanced_is_valid_and_notes_the_tenant_columns(self):
        errors, notes, values = run(make("advanced"))
        self.assertEqual(errors, [])
        self.assertEqual(values["tenancy.key"], "guid")
        self.assertNotIn("storage", values["addons"])
        self.assertTrue(any(note.startswith("R7") for note in notes))

    def test_absent_keys_take_the_presets_value(self):
        text = make("standard")
        kept = "\n".join(line for line in text.splitlines() if not line.startswith("| exceptions |"))
        errors, notes, values = run(kept)
        self.assertEqual(errors, [])
        self.assertEqual(values["exceptions"], "coded")


class RuleTests(unittest.TestCase):
    def assert_rule(self, rule, text):
        errors, notes, values = run(text)
        self.assertIn(rule, rules_in(errors), errors)

    def test_r1_outbox_and_transport_go_together(self):
        self.assert_rule("R1", make("lean", events__transport="database"))
        self.assert_rule("R1", make("standard", events__outbox="none"))

    def test_r2_rabbitmq_needs_an_outbox(self):
        self.assert_rule("R2", make("lean", events__transport="rabbitmq"))

    def test_r3_outbox_needs_a_worker(self):
        self.assert_rule("R3", make("standard", hosts__worker="none"))

    def test_r4_rabbitmq_needs_a_separate_worker(self):
        self.assert_rule("R4", make("advanced", hosts__worker="in-api"))

    def test_r5_translation_needs_coded_exceptions(self):
        self.assert_rule("R5", make("standard", exceptions="minimal", languages__mode="messages"))

    def test_r6_default_language_must_be_supported(self):
        self.assert_rule("R6", make("standard", languages__mode="messages", languages__default="tr", languages__supported="en, de"))

    def test_r7_tenant_columns_are_a_note_not_an_error(self):
        errors, notes, values = run(make("advanced"))
        self.assertEqual(errors, [])
        self.assertTrue(any(note.startswith("R7") for note in notes))

    def test_r8_mongo_replica_set_is_a_note_not_an_error(self):
        errors, notes, values = run(make("standard", database="mongo"))
        self.assertEqual(errors, [])
        self.assertTrue(any(note.startswith("R8") for note in notes))

    def test_r9_scheduler_needs_a_worker(self):
        self.assert_rule("R9", make("lean", addons="scheduler"))

    def test_r10_read_models_need_an_outbox(self):
        self.assert_rule("R10", make("lean", queries="read-models"))

    def test_r11_go_is_refused_until_it_is_available(self):
        self.assert_rule("R11", make("lean", language="go"))

    def test_r12_mongo_has_no_integer_keys(self):
        self.assert_rule("R12", make("standard", database="mongo", keys__default="int"))

    def test_r13_tenancy_needs_signed_in_callers(self):
        self.assert_rule("R13", make("advanced", auth="none"))

    def test_every_rule_in_the_schema_has_a_test(self):
        ids = {rule["id"] for rule in decisions.load_schema()["rules"]}
        source = Path(__file__).read_text(encoding="utf-8")
        for rule_id in ids:
            self.assertIn(f"test_{rule_id.lower()}_", source, rule_id)


class ParserTests(unittest.TestCase):
    def test_prose_edited_by_hand_is_ignored(self):
        text = "Some notes first.\n\n" + make("standard") + "\n## My own section\n\n| Key | Value | Why |\n|---|---|---|\n| nonsense | row | x |\n"
        errors, notes, values = run(text)
        self.assertEqual(errors, [])

    def test_duplicate_key_names_the_row(self):
        text = make("standard") + ""
        lines = text.splitlines()
        index = next(i for i, line in enumerate(lines) if line.startswith("| database |"))
        lines.insert(index + 1, "| database | mongo | |")
        errors, notes, values = run("\n".join(lines))
        duplicate = [e for e in errors if "duplicate" in e["message"]]
        self.assertEqual(len(duplicate), 1)
        self.assertEqual(duplicate[0]["key"], "database")
        self.assertEqual(duplicate[0]["line"], index + 2)

    def test_unknown_key_names_the_row(self):
        errors, notes, values = run(make("standard", flavour="spicy"))
        unknown = [e for e in errors if e["message"] == "unknown key"]
        self.assertEqual([e["key"] for e in unknown], ["flavour"])
        self.assertGreater(unknown[0]["line"], 0)

    def test_unknown_value_names_the_row(self):
        errors, notes, values = run(make("standard", exceptions="elaborate"))
        unknown = [e for e in errors if "unknown value" in e["message"]]
        self.assertEqual([e["key"] for e in unknown], ["exceptions"])

    def test_missing_heading_is_reported(self):
        errors, notes, values = run("# Nothing here\n")
        self.assertEqual(len(errors), 1)

    def test_missing_required_key_is_reported(self):
        text = "\n".join(line for line in make("standard").splitlines() if not line.startswith("| database |"))
        errors, notes, values = run(text)
        self.assertIn("database", [e["key"] for e in errors])

    def test_conditional_key_without_its_parent_is_refused(self):
        errors, notes, values = run(make("standard", tenancy__key="guid"))
        self.assertIn("tenancy.key", [e["key"] for e in errors])

    def test_unknown_addon_is_refused(self):
        errors, notes, values = run(make("standard", addons="correlation, magic"))
        self.assertIn("addons", [e["key"] for e in errors])

    def test_why_column_may_contain_pipes_and_is_ignored(self):
        text = make("standard").replace("| database | postgres | |", "| database | postgres | one | two |")
        errors, notes, values = run(text)
        self.assertEqual(errors, [])


if __name__ == "__main__":
    unittest.main()
