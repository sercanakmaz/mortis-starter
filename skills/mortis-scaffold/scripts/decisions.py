import json
import re
import sys
from pathlib import Path

SCHEMA_PATH = Path(__file__).resolve().parent.parent / "references" / "schema.json"
HEADING = "## Decisions"


def load_schema(path=SCHEMA_PATH):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def parse(text):
    rows = []
    errors = []
    lines = text.splitlines()
    start = None
    for number, line in enumerate(lines):
        if line.strip() == HEADING:
            start = number
            break
    if start is None:
        return rows, [{"line": 0, "key": "", "message": f"no '{HEADING}' heading"}]
    number = start + 1
    while number < len(lines) and not lines[number].lstrip().startswith("|"):
        if lines[number].startswith("#"):
            break
        number += 1
    if number >= len(lines) or not lines[number].lstrip().startswith("|"):
        return rows, [{"line": start + 1, "key": "", "message": "no table under the Decisions heading"}]
    header = [cell.strip().lower() for cell in lines[number].strip().strip("|").split("|")]
    if header[:2] != ["key", "value"]:
        return rows, [{"line": number + 1, "key": "", "message": "the table must start with the columns Key and Value"}]
    number += 2
    seen = {}
    while number < len(lines) and lines[number].lstrip().startswith("|"):
        cells = lines[number].strip().strip("|").split("|")
        key = cells[0].strip()
        if len(cells) < 2 or not key:
            errors.append({"line": number + 1, "key": key, "message": "a row needs a key and a value"})
        else:
            value = cells[1].strip()
            why = "|".join(cells[2:]).strip()
            if key in seen:
                errors.append({"line": number + 1, "key": key, "message": f"duplicate key (first on line {seen[key]})"})
            else:
                seen[key] = number + 1
                rows.append({"line": number + 1, "key": key, "value": value, "why": why})
        number += 1
    return rows, errors


def split_list(value):
    return [item.strip() for item in value.split(",") if item.strip()]


def condition_holds(spec, values):
    for key, allowed in spec.get("requires", {}).items():
        if values.get(key) not in allowed:
            return False
    return True


def resolve(rows, schema):
    errors = []
    given = {}
    for row in rows:
        key = row["key"]
        if key not in schema["keys"]:
            errors.append({"line": row["line"], "key": key, "rule": "", "message": "unknown key", "fix": "Remove the row or fix the spelling; the valid keys are in references/schema.json."})
            continue
        given[key] = row
    values = {}
    for key, row in given.items():
        spec = schema["keys"][key]
        raw = row["value"]
        value = split_list(raw) if spec["kind"] == "list" else raw
        values[key] = value
    preset = values.get("preset")
    for key, spec in schema["keys"].items():
        if key in values:
            continue
        if "presets" in spec and preset in spec["presets"]:
            values[key] = spec["presets"][preset]
        elif "default" in spec and "requires" not in spec:
            values[key] = spec["default"]
    for key, spec in schema["keys"].items():
        if key in values:
            continue
        if "requires" in spec and condition_holds(spec, values) and "default" in spec:
            values[key] = spec["default"]
    return given, values, errors


def check_value(key, spec, value, row, schema, errors):
    line = row["line"] if row else 0
    kind = spec["kind"]
    allowed = spec.get("values")
    if kind == "language":
        if value not in schema["languages"]:
            errors.append({"line": line, "key": key, "rule": "", "message": f"unknown language '{value}'", "fix": "Use one of: " + ", ".join(schema["languages"])})
    elif kind == "enum":
        if value not in allowed:
            errors.append({"line": line, "key": key, "rule": "", "message": f"unknown value '{value}'", "fix": "Use one of: " + ", ".join(allowed)})
    elif kind == "text":
        if not re.match(spec["pattern"], value or "") if "pattern" in spec else not value:
            errors.append({"line": line, "key": key, "rule": "", "message": f"'{value}' is not an accepted {key}", "fix": f"Match {spec.get('pattern', 'a non-empty value')}"})
    elif kind == "list":
        for item in value:
            if allowed is not None and item not in allowed:
                errors.append({"line": line, "key": key, "rule": "", "message": f"unknown value '{item}'", "fix": "Use any of: " + ", ".join(allowed)})
            if "pattern" in spec and not re.match(spec["pattern"], item):
                errors.append({"line": line, "key": key, "rule": "", "message": f"'{item}' is not an accepted language tag", "fix": f"Match {spec['pattern']}"})
        if len(set(value)) != len(value):
            errors.append({"line": line, "key": key, "rule": "", "message": "duplicate entry in the list", "fix": "List each value once."})


def rule_error(schema, rule_id, line, key, message):
    fix = next(rule["fix"] for rule in schema["rules"] if rule["id"] == rule_id)
    return {"line": line, "key": key, "rule": rule_id, "message": message, "fix": fix}


def check_rules(schema, values, lines):
    errors = []
    notes = []
    outbox = values.get("events.outbox")
    transport = values.get("events.transport")
    worker = values.get("hosts.worker")

    def at(key):
        return lines.get(key, 0)

    if (outbox == "none") != (transport == "in-process"):
        errors.append(rule_error(schema, "R1", at("events.transport") or at("events.outbox"), "events.outbox", f"events.outbox = {outbox} does not go with events.transport = {transport}"))
    if transport == "rabbitmq" and outbox == "none":
        errors.append(rule_error(schema, "R2", at("events.transport"), "events.transport", "rabbitmq needs an outbox"))
    if outbox in ("after-command", "background") and worker == "none":
        errors.append(rule_error(schema, "R3", at("hosts.worker"), "hosts.worker", f"events.outbox = {outbox} needs a worker host"))
    if transport == "rabbitmq" and worker != "separate":
        errors.append(rule_error(schema, "R4", at("hosts.worker"), "hosts.worker", "rabbitmq needs hosts.worker = separate"))
    if values.get("languages.mode") not in (None, "none") and values.get("exceptions") == "minimal":
        errors.append(rule_error(schema, "R5", at("exceptions"), "exceptions", "translated messages need coded or catalogued exceptions"))
    if "languages.default" in values and values["languages.default"] not in values.get("languages.supported", []):
        errors.append(rule_error(schema, "R6", at("languages.default"), "languages.default", "the default language is not in languages.supported"))
    if values.get("tenancy.mode") == "interface" and outbox in ("after-command", "background"):
        notes.append("R7: the tenant is added to outbox and inbox rows by the scaffold")
    if values.get("database") == "mongo" and outbox in ("after-command", "background"):
        notes.append("R8: the generated compose file starts a MongoDB replica set")
    if "scheduler" in values.get("addons", []) and worker == "none":
        errors.append(rule_error(schema, "R9", at("addons"), "addons", "scheduler needs a worker host"))
    if values.get("queries") == "read-models" and outbox == "none":
        errors.append(rule_error(schema, "R10", at("queries"), "queries", "read models are fed from events and need an outbox"))
    language = values.get("language")
    if language in schema["languages"] and not schema["languages"][language]["available"]:
        errors.append(rule_error(schema, "R11", at("language"), "language", f"language '{language}' has no references yet"))
    if values.get("database") == "mongo" and values.get("keys.default") in ("int", "long"):
        errors.append(rule_error(schema, "R12", at("keys.default"), "keys.default", "mongo does not support integer keys"))
    return errors, notes


def validate_text(text, schema=None):
    schema = schema or load_schema()
    rows, errors = parse(text)
    if errors and not rows:
        return [dict(e, rule="", fix="Add a '## Decisions' heading followed by the table.") for e in errors], [], {}
    for error in errors:
        error.update(rule="", fix="Keep one row per key, each with a key and a value.")
    given, values, resolve_errors = resolve(rows, schema)
    errors.extend(resolve_errors)
    for key, row in given.items():
        check_value(key, schema["keys"][key], values[key], row, schema, errors)
    for key, spec in schema["keys"].items():
        if spec.get("required") and key not in values:
            errors.append({"line": 0, "key": key, "rule": "", "message": "required key is missing", "fix": f"Add a row for {key}."})
        if key in given and "requires" in spec and not condition_holds(spec, values):
            conditions = ", ".join(f"{k} in {v}" for k, v in spec["requires"].items())
            errors.append({"line": given[key]["line"], "key": key, "rule": "", "message": f"only applies when {conditions}", "fix": "Remove the row or change the key it depends on."})
    if not errors:
        rule_errors, notes = check_rules(schema, values, {k: r["line"] for k, r in given.items()})
        errors.extend(rule_errors)
    else:
        notes = []
    return errors, notes, values


def load(path, schema=None):
    return validate_text(Path(path).read_text(encoding="utf-8"), schema)


def main(argv):
    if len(argv) == 4 and argv[2] == "get":
        errors, notes, values = load(argv[1])
        if errors or argv[3] not in values:
            return 1
        value = values[argv[3]]
        print(",".join(value) if isinstance(value, list) else value)
        return 0
    print("usage: decisions.py MORTIS.md get <key>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
