import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCAFFOLD = os.path.join(ROOT, "skills", "mortis-scaffold")
VARIANTS = os.path.join(ROOT, "_temp", "variants")
PRESETS = ("lean", "advanced", "standard")
TEMPLATE = VARIANTS
CSHARP = os.path.join(SCAFFOLD, "references", "csharp")


def source_path(relative):
    return next((os.path.join(VARIANTS, p, relative) for p in PRESETS if os.path.exists(os.path.join(VARIANTS, p, relative))), os.path.join(VARIANTS, PRESETS[0], relative))


SUPERSEDED = re.compile(r"(Domain/Aggregates/\w+/\w+(Service|Module)\.cs|TestSupport/Domain/\w+/\w+ServiceBuilder\.cs|Tests/Domain/\w+/\w+ServiceTests\.cs)$")
ONE_WAY = re.compile(r"\b(alternatively|you may also|you can also|or use|optionally use)\b", re.I)

sys.path.insert(0, os.path.join(SCAFFOLD, "scripts", "lang"))
from csharp_shape import mask, shape_of_file  # noqa: E402

failures = []


def fail(message):
    failures.append(message)


def template_files():
    found = set()
    for preset in PRESETS:
        root = os.path.join(VARIANTS, preset)
        for base, dirs, files in os.walk(root):
            dirs[:] = [d for d in dirs if d not in ("bin", "obj", ".mortis")]
            for name in files:
                relative = os.path.relpath(os.path.join(base, name), root).replace(os.sep, "/")
                if relative != "MORTIS.md":
                    found.add(relative)
    return sorted(found)


def manifest():
    return json.load(open(os.path.join(CSHARP, "manifest.json"), encoding="utf-8"))


def built_nowhere(entry):
    return entry.get("unbuilt", False)


def check_manifest_covers_template():
    if not os.path.isdir(TEMPLATE):
        return
    paths = {e["path"] for e in manifest()["files"]}
    for f in template_files():
        if "/Migrations/" in f or SUPERSEDED.search(f):
            continue
        if f.replace("Boilerplate", "{Name}") not in paths:
            fail(f"manifest.json has no entry for the variant file {f}")
    for entry in manifest()["files"]:
        if not os.path.exists(os.path.join(CSHARP, entry["page"])):
            fail(f"manifest.json names a page that does not exist: {entry['page']}")


def check_kernel_shape():
    if not os.path.isdir(TEMPLATE):
        return
    kernel = [e["path"].replace("{Name}", "Boilerplate") for e in manifest()["files"] if e["kernel"] and not built_nowhere(e)]
    for preset in ("lean", "standard", "advanced"):
        expected = []
        for f in kernel:
            expected += shape_of_file(os.path.join(VARIANTS, preset, f), "Boilerplate")
        written = subprocess.run([sys.executable, "-I", os.path.join(SCAFFOLD, "scripts", "shape_for.py"), os.path.join(CSHARP, "kernel-shape.txt"), os.path.join(VARIANTS, preset, "MORTIS.md")],
                                 capture_output=True, text=True, check=True).stdout.splitlines()
        if expected != written:
            missing = [l for l in expected if l not in written][:5]
            extra = [l for l in written if l not in expected][:5]
            fail(f"kernel-shape.txt differs from the {preset} variant's kernel; missing {missing}, extra {extra}")
    if False:
        missing = [l for l in expected if l not in written][:5]
        extra = [l for l in written if l not in expected][:5]
        fail(f"kernel-shape.txt differs from the lean variant's kernel; missing {missing}, extra {extra}")


def code_blocks():
    blocks = []
    for name in sorted(os.listdir(CSHARP)):
        if name.endswith(".md"):
            text = open(os.path.join(CSHARP, name), encoding="utf-8").read()
            blocks += [(name, b) for b in re.findall(r"```csharp\n(.*?)```", text, re.S)]
    return blocks


def non_trivial_members(path):
    text = open(path, encoding="utf-8").read()
    masked = mask(text)
    for match in re.finditer(r"^[ \t]*(?:public|protected)[^\n;=]*?\b(\w+)\s*(?:<[^>\n]*>)?\s*\([^)]*\)[^\n{;]*\n?\s*\{", masked, re.M):
        depth = 0
        i = masked.index("{", match.start())
        start = i
        while i < len(masked):
            depth += masked[i] == "{"
            depth -= masked[i] == "}"
            i += 1
            if depth == 0:
                break
        if masked[start:i].count(";") > 1:
            yield match.group(1), masked[match.start():masked.index("{", match.start())].strip()


def check_snippets_cover_members():
    if not os.path.isdir(TEMPLATE):
        return
    blocks = "\n".join(b for _, b in code_blocks())
    for entry in manifest()["files"]:
        if not entry["kernel"] or built_nowhere(entry):
            continue
        path = source_path(entry["path"].replace("{Name}", "Boilerplate"))
        if not path.endswith(".cs") or os.path.basename(path) == "Program.cs":
            continue
        for name, head in non_trivial_members(path):
            signature = re.sub(r"\s+", " ", head.split("(")[0]).strip()
            if not any(re.sub(r"\s+", " ", line).strip().startswith(signature) for line in blocks.splitlines()):
                fail(f"{entry['path']}: the member {name} has a body of several statements and no snippet")


def check_one_way():
    for skill in ("mortis-scaffold", "mortis-architect", "mortis-develop", "mortis-review"):
        for base, _, files in os.walk(os.path.join(ROOT, "skills", skill)):
            for name in files:
                if name.endswith(".md"):
                    path = os.path.join(base, name)
                    for number, line in enumerate(open(path, encoding="utf-8"), 1):
                        if ONE_WAY.search(line):
                            fail(f"{os.path.relpath(path, ROOT)}:{number}: offers an alternative ('{ONE_WAY.search(line).group(0)}'); a page gives one way")


def check_copies():
    pairs = [("skills/mortis-architect/scripts/decisions.py", "skills/mortis-scaffold/scripts/decisions.py"),
             ("skills/mortis-architect/references/schema.json", "skills/mortis-scaffold/references/schema.json")]
    for a, b in pairs:
        if open(os.path.join(ROOT, a), encoding="utf-8").read() != open(os.path.join(ROOT, b), encoding="utf-8").read():
            fail(f"{b} is not an exact copy of {a}")


ARCHITECT = os.path.join(ROOT, "skills", "mortis-architect")


def read_option_sections():
    folder = os.path.join(ARCHITECT, "references", "options")
    sections = {}
    for name in sorted(os.listdir(folder)):
        current = None
        for line in open(os.path.join(folder, name), encoding="utf-8"):
            if line.startswith("## "):
                current = line[3:].strip()
                sections[current] = set()
            elif line.startswith("### ") and current:
                sections[current].add(line[4:].strip())
    return sections


def cell_values(cell):
    cell = cell.strip()
    if cell.lower() in ("none", ""):
        return []
    return [part.strip().strip("`") for part in cell.split(",")]


def check_decisions_schema():
    schema = json.load(open(os.path.join(ARCHITECT, "references", "schema.json"), encoding="utf-8"))
    sections = read_option_sections()
    for key, spec in schema["keys"].items():
        if key not in sections:
            fail(f"references/options has no '## {key}' section")
            continue
        values = spec.get("values", []) if spec["kind"] in ("enum", "list") else []
        for value in values:
            if value not in sections[key]:
                fail(f"references/options: '## {key}' has no '### {value}' section")
    spec_text = open(os.path.join(ROOT, "docs", "specs", "decisions-file.md"), encoding="utf-8").read()
    rule_ids = set(re.findall(r"^\| (R\d+) \|", spec_text, re.M))
    schema_ids = {rule["id"] for rule in schema["rules"]}
    if rule_ids != schema_ids:
        fail(f"decisions-file.md rules {sorted(rule_ids)} differ from schema.json rules {sorted(schema_ids)}")
    for line in spec_text.splitlines():
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) != 5 or not cells[0].startswith("`"):
            continue
        key = cells[0].strip("`")
        if key not in schema["keys"] or "presets" not in schema["keys"][key]:
            continue
        for index, preset in enumerate(("lean", "standard", "advanced"), start=2):
            expected = schema["keys"][key]["presets"][preset]
            expected = expected if isinstance(expected, list) else [expected]
            cell = cells[index]
            if cell.startswith("all but"):
                excluded = {part.strip("` ") for part in cell[len("all but"):].split(",")}
                actual = [v for v in schema["keys"][key]["values"] if v not in excluded]
            else:
                actual = cell_values(cell)
            if sorted(actual) != sorted(expected):
                fail(f"decisions-file.md: {key} / {preset} is {actual}, schema.json says {expected}")
    for key in schema["keys"]:
        if f"`{key}`" not in spec_text:
            fail(f"decisions-file.md does not mention the key {key}")


DEVELOP = os.path.join(ROOT, "skills", "mortis-develop", "references")
TAG = re.compile(r"\[([\w.]+)(!?=)([^\]]+)\]")


def check_recipe_tags():
    schema = json.load(open(os.path.join(ARCHITECT, "references", "schema.json"), encoding="utf-8"))
    pages = set(os.listdir(CSHARP)) | set(os.listdir(os.path.join(DEVELOP, "recipes"))) | {"adr-template.md", "kernel-shape.txt"}
    recipes = sorted(os.path.join(DEVELOP, "recipes", name) for name in os.listdir(os.path.join(DEVELOP, "recipes")))
    for path in recipes + [os.path.join(DEVELOP, "where-else.md")]:
        relative = os.path.relpath(path, ROOT)
        for number, line in enumerate(open(path, encoding="utf-8"), 1):
            for key, operator, raw in TAG.findall(line.replace("\\|", "|")):
                spec = schema["keys"].get(key)
                if spec is None:
                    fail(f"{relative}:{number}: the tag [{key}{operator}{raw}] names no key of schema.json")
                    continue
                allowed = spec.get("values")
                for value in raw.split("|"):
                    if allowed is not None and value not in allowed:
                        fail(f"{relative}:{number}: the tag [{key}{operator}{raw}]: {value} is not a value of {key}")
            if path in recipes and line.startswith("| ") and not line.startswith("| #") and not line.startswith("|---"):
                cells = [cell.strip() for cell in line.replace("\\|", "/").strip().strip("|").split("|")]
                if len(cells) != 5:
                    fail(f"{relative}:{number}: a recipe row has {len(cells)} cells, not five (#, file, page, rule, test)")
                    continue
                for page in re.findall(r"`([\w.-]+\.(?:md|txt))`", cells[2]):
                    if page not in pages and not page.startswith("../"):
                        fail(f"{relative}:{number}: the page {page} does not exist")
                if not cells[3]:
                    fail(f"{relative}:{number}: a recipe row names no rule")


def main():
    check_manifest_covers_template()
    check_kernel_shape()
    check_snippets_cover_members()
    check_one_way()
    check_copies()
    check_decisions_schema()
    check_recipe_tags()
    if failures:
        for message in failures:
            print("DOC-MISMATCH", message)
        print(f"{len(failures)} mismatch(es)")
        return 1
    print("docs match the variants and the example")
    return 0


sys.exit(main())
