import argparse
import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("decisions", HERE / "decisions.py")
decisions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(decisions)

INTRO = """# Project decisions

This project was started with mortis-starter. The table below records what was chosen. Skills and checks read it; change a row here before changing the code it describes.

## Decisions

| Key | Value | Why |
|---|---|---|
"""

OUTRO = """
## Working in this project

| To do this | Use |
|---|---|
| Add an aggregate, command, query, event or handler | the `mortis-develop` skill |
| Check a change before merging | the `mortis-review` skill |
| Turn an option on or change one | edit the table above, then ask `mortis-develop` to apply it |
| Verify the whole project | `verify.sh` from the `mortis-scaffold` skill |

The rules of the code are in [AGENTS.md](AGENTS.md); how it works is in [docs/architecture.md](docs/architecture.md); why each non-default choice was made is in [docs/adr/](docs/adr/).
"""


def build(name, language, database, preset, overrides, reasons=None):
    reasons = reasons or {}
    schema = decisions.load_schema()
    values = {"version": "1", "name": name, "language": language, "preset": preset, "database": database}
    for key, spec in schema["keys"].items():
        if key in values:
            continue
        if "presets" in spec:
            value = spec["presets"][preset]
            values[key] = ", ".join(value) if isinstance(value, list) else value
    values.update(overrides)
    if values.get("tenancy.mode") == "interface":
        values.setdefault("tenancy.key", schema["keys"]["tenancy.key"]["default"])
        values.setdefault("tenancy.resolver", schema["keys"]["tenancy.resolver"]["default"])
    if values.get("languages.mode") in ("messages", "data"):
        values.setdefault("languages.default", "en")
        values.setdefault("languages.supported", "en")
    order = [key for key in schema["keys"] if key in values] + [key for key in values if key not in schema["keys"]]
    rows = "".join(f"| {key} | {values[key]} | {reasons.get(key, '')} |\n" for key in order)
    return INTRO + rows + OUTRO


def main(argv):
    parser = argparse.ArgumentParser(description="Write a preset's MORTIS.md")
    parser.add_argument("--name", required=True)
    parser.add_argument("--language", required=True)
    parser.add_argument("--database", required=True)
    parser.add_argument("--preset", default="standard", choices=["lean", "standard", "advanced"])
    parser.add_argument("--set", action="append", default=[], metavar="KEY=VALUE")
    parser.add_argument("--why", action="append", default=[], metavar="KEY=REASON")
    parser.add_argument("--out", default="MORTIS.md")
    args = parser.parse_args(argv[1:])
    overrides = dict(item.split("=", 1) for item in args.set)
    reasons = dict(item.split("=", 1) for item in args.why)
    text = build(args.name, args.language, args.database, args.preset, overrides, reasons)
    errors, notes, values = decisions.validate_text(text)
    if errors:
        for error in errors:
            print(f"{error['key']}: {error['message']}. Fix: {error['fix']}", file=sys.stderr)
        return 1
    Path(args.out).write_text(text, encoding="utf-8")
    print(f"wrote {args.out}")
    for note in notes:
        print(f"note {note}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
