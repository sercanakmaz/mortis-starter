import fnmatch
import importlib.util
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("decisions", HERE / "decisions.py")
decisions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(decisions)

SKIP_PARTS = {"bin", "obj", ".git", ".vs", ".idea", "TestResults", "node_modules", ".mortis"}


def pattern_to_regex(pattern, name):
    text = pattern.replace("{Name}", name)
    out = ""
    i = 0
    while i < len(text):
        if text.startswith("**", i):
            out += ".*"
            i += 2
        elif text[i] == "*":
            out += "[^/]*"
            i += 1
        elif text.startswith("{plural}", i):
            out += "[a-z][a-z0-9]*"
            i += len("{plural}")
        elif text.startswith("{Plural}", i) or text.startswith("{Aggregate}", i):
            token = text[i:text.index("}", i) + 1]
            out += "[A-Z][A-Za-z0-9]*"
            i += len(token)
        else:
            out += re.escape(text[i])
            i += 1
    return re.compile("^" + out + "$")


def applies(entry, values):
    if "when_not" in entry and applies({"when": entry["when_not"]}, values):
        return False
    if "when_any" in entry:
        return any(applies({"when": when}, values) for when in entry["when_any"])
    for key, expected in entry.get("when", {}).items():
        actual = values.get(key)
        if isinstance(expected, list) and isinstance(actual, list):
            if not all(item in actual for item in expected):
                return False
        elif isinstance(expected, list):
            if actual not in expected:
                return False
        elif isinstance(actual, list):
            if expected not in actual:
                return False
        elif actual != expected:
            return False
    return True


def singular(plural):
    return plural[:-3] + "y" if plural.endswith("ies") else plural[:-1] if plural.endswith("s") else plural


def with_event_naming(entry, values):
    if values.get("naming.events") != "prefixed":
        return entry
    match = re.search(r"/(?:Aggregates/(\w+)/Events|\{Name\}\.Contracts/(\w+))/(\w+)\.cs$", entry["path"])
    if not match or "{" in match.group(3):
        return entry
    plural = match.group(1) or match.group(2)
    return {**entry, "path": entry["path"][:match.start(3)] + singular(plural) + match.group(3) + ".cs"}


def project_files(root):
    for path in sorted(Path(root).rglob("*")):
        if path.is_file() and not (SKIP_PARTS & set(path.relative_to(root).parts)):
            yield path.relative_to(root).as_posix()


def check(root, plan_only=False):
    root = Path(root)
    errors, notes, values = decisions.load(root / "MORTIS.md")
    if errors:
        return [f"MORTIS.md: {e['key']}: {e['message']}" for e in errors]
    language = values["language"]
    name = values["name"]
    manifest = json.loads((HERE.parent / "references" / language / "manifest.json").read_text(encoding="utf-8"))
    entries = [with_event_naming(e, values) for e in manifest["files"]]
    expected = [e for e in entries if applies(e, values)]
    known = list(entries)
    if plan_only:
        return [f"{len(expected)} files expected from the manifest for this MORTIS.md"]
    problems = []
    files = set(project_files(root))
    for entry in expected:
        if entry.get("unbuilt"):
            continue
        regex = pattern_to_regex(entry["path"], name)
        if not any(regex.match(f) for f in files):
            problems.append(f"missing: {entry['path'].replace('{Name}', name)} (see references/{language}/{entry['page']})")
    allowed = [pattern_to_regex(e["path"], name) for e in expected]
    allowed += [pattern_to_regex(p, name) for p in manifest.get("always_allowed", {}).get("patterns", [])]
    for folder, patterns in manifest.get("aggregate_files", {}).get("folders", {}).items():
        for pattern in patterns:
            allowed.append(pattern_to_regex(folder + "/" + pattern, name))
    excluded = [pattern_to_regex(e["path"], name) for e in known if e not in expected]
    for f in sorted(files):
        if any(r.match(f) for r in excluded) and not any(pattern_to_regex(e["path"], name).match(f) for e in expected):
            problems.append(f"extra: {f} belongs to an option this MORTIS.md does not turn on")
        elif not any(r.match(f) for r in allowed):
            problems.append(f"extra: {f} is in no manifest entry and no allowed folder")
    return problems


def main(argv):
    if len(argv) < 2:
        print("usage: structure.py <project-dir> [--plan]", file=sys.stderr)
        return 2
    problems = check(argv[1], plan_only="--plan" in argv)
    for problem in problems:
        print(problem)
    if "--plan" in argv:
        return 0
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
