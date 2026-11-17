import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKILL = os.path.join(ROOT, "skills", "mortis-scaffold")
ASSETS = os.path.join(ROOT, "_temp", "template", "assets")
LANGS = sorted(name for name in os.listdir(ASSETS) if os.path.isdir(os.path.join(ASSETS, name)))
FILE_RE = re.compile(r"^[\w.\-/]+\.(cs|csproj|json|md|yml|props|txt|sh|py|slnx)$")
NAMED_FILES = {"Dockerfile", ".editorconfig", ".gitignore"}
TYPE_SUFFIXES = ("Command", "CommandHandler", "Service", "Repository", "Dto", "Module", "Filter", "Scenario",
                 "Middleware", "Constants", "Builder", "Handler", "Exception", "Dispatcher", "Extensions",
                 "Configuration", "Executor", "Pipeline", "Factory")

failures = []


def fail(message):
    failures.append(message)


def walk(base):
    for directory, directories, files in os.walk(base):
        directories[:] = [name for name in directories if name not in ("bin", "obj")]
        for name in files:
            yield os.path.relpath(os.path.join(directory, name), base)


def normalized(path):
    return path.replace("Boilerplate.", "")


def example_prefixes(lang):
    prefixes = []
    manifest = os.path.join(ASSETS, f"{lang}-example.txt")
    with open(manifest, encoding="utf-8") as handle:
        for line in handle:
            parts = line.split()
            if len(parts) == 2 and parts[0] == "delete":
                prefixes.append(normalized(parts[1]))
    return prefixes


def build_universe(lang):
    everything = {normalized(path): path for path in walk(os.path.join(ASSETS, lang))}
    prefixes = example_prefixes(lang)
    example = {path: original for path, original in everything.items() if any(path == prefix or path.startswith(prefix + "/") for prefix in prefixes)}
    template = {path: original for path, original in everything.items() if path not in example}
    return template, example


def declared_types(lang):
    names = set()
    pattern = re.compile(r"\b(?:class|record|interface|struct|enum)\s+([A-Z]\w*)")
    base = os.path.join(ASSETS, lang)
    for path in walk(base):
        if path.endswith(".cs"):
            with open(os.path.join(base, path), encoding="utf-8") as handle:
                names.update(pattern.findall(handle.read()))
    return names


def skill_doc_paths():
    paths = [os.path.join(SKILL, "SKILL.md")]
    references = os.path.join(SKILL, "references")
    paths += [os.path.join(references, name) for name in sorted(os.listdir(references)) if name.endswith(".md")]
    return paths


def exists_anywhere(token, universe):
    candidate = normalized(token)
    if candidate.startswith("./"):
        candidate = candidate[2:]
    for path in universe:
        if path == candidate or path.endswith("/" + candidate):
            return True
    return False


def resolves_in_repo(token):
    for base in (SKILL, ROOT, os.path.join(ROOT, "_temp", "template")):
        if os.path.exists(os.path.join(base, token)):
            return True
    return False


def check_tokens(lang, universe, types):
    for doc in skill_doc_paths():
        label = os.path.relpath(doc, ROOT)
        with open(doc, encoding="utf-8") as handle:
            text = handle.read()
        text_without_fences = re.sub(r"```.*?```", "", text, flags=re.S)
        for token in sorted(set(re.findall(r"`([^`\n]+)`", text_without_fences))):
            if any(character in token for character in "<>*$ {}|=:,()'\"@"):
                continue
            if token.startswith(("-", "http", "dotnet", "docker")):
                continue
            if FILE_RE.match(token) or token in NAMED_FILES:
                if token.startswith(("assets/", "scripts/", "references/", "tests/", "skills/")):
                    if not resolves_in_repo(token):
                        fail(f"{label}: path `{token}` does not exist")
                elif "/" in token or token.endswith((".cs", ".csproj")):
                    if not exists_anywhere(token, universe):
                        fail(f"{label}: file `{token}` is in neither the template nor the example")
            elif re.match(r"^[A-Z][A-Za-z0-9]+$", token) and token.endswith(TYPE_SUFFIXES) and token not in TYPE_SUFFIXES:
                if token not in types and token not in KNOWN_NON_TYPES:
                    fail(f"{label}: type `{token}` is declared nowhere in the template or the example")


KNOWN_NON_TYPES = {
    "AddCommandScenario", "AddScoped", "DbContext", "IDesignTimeModel", "ApplicationModule", "InfrastructureModule",
    "DomainModule", "SharedModule", "PersistenceModule", "ScenarioModule", "FluentValidation", "AggregateConfiguration",
    "ChildEntityConfiguration", "AbstractValidator", "IEntityTypeConfiguration", "DbUpdateConcurrencyException",
    "ComplexProperty", "UpdateAsync", "CreateAsync", "DeleteAsync", "DbSet", "IUserContext",
}


def parse_tree(text):
    match = re.search(r"## Where every file lives.*?```\n(.*?)```", text, flags=re.S)
    if not match:
        fail("architecture-overview.md has no 'Where every file lives' tree")
        return []
    entries = []
    current = ""
    for line in match.group(1).splitlines():
        if not line.strip():
            continue
        if line.startswith("repo root"):
            current = ""
            line = line[len("repo root"):]
        elif not line.startswith(" ") and line.split()[0].endswith("/"):
            current = line.split()[0]
            line = line[len(current):]
        chunks = [chunk.strip() for chunk in re.split(r"\s{2,}", line.strip()) if chunk.strip()]
        for chunk in chunks:
            if chunk.startswith("(") or " " in chunk:
                continue
            if FILE_RE.match(chunk) or chunk in NAMED_FILES:
                entries.append(chunk if "/" in chunk else current + chunk)
    return entries


def check_tree(template, example):
    with open(os.path.join(SKILL, "references", "architecture-overview.md"), encoding="utf-8") as handle:
        text = handle.read()
    entries = parse_tree(text)
    listed = set()
    for entry in entries:
        if entry.startswith("test/Tests/TestSupport") or entry.startswith("test/Tests/"):
            continue
        listed.add(entry)
        if entry not in template:
            fail(f"architecture-overview.md lists {entry}, which is not in the template")
    for path in template:
        if path.startswith("src/") and path.endswith((".cs", ".json")) and path not in listed:
            fail(f"architecture-overview.md does not list the template file {path}")
    root_listed = {entry for entry in entries if "/" not in entry}
    for path in template:
        if "/" not in path and path.endswith(("Dockerfile", ".md", ".props", ".json", "docker-compose.yml", ".txt", ".editorconfig")) and path not in root_listed:
            if path not in ("Boilerplate.slnx", "slnx", ".gitignore"):
                fail(f"architecture-overview.md does not list the root file {path}")


def check_example_coverage(example):
    docs = ""
    for doc in skill_doc_paths():
        with open(doc, encoding="utf-8") as handle:
            docs += handle.read()
    aggregates = sorted({path.split("/")[3] for path in example if path.startswith("src/Domain/Aggregates/")})
    for aggregate in aggregates:
        if f"`{aggregate}`" not in docs and aggregate not in docs:
            fail(f"no reference mentions the example aggregate folder {aggregate}")


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


def main():
    for lang in LANGS:
        template, example = build_universe(lang)
        universe = set(template) | set(example)
        types = declared_types(lang)
        check_tokens(lang, universe, types)
        check_tree(template, example)
        check_example_coverage(example)
    check_decisions_schema()
    if failures:
        for message in failures:
            print("DOC-MISMATCH", message)
        print(f"{len(failures)} mismatch(es)")
        return 1
    print("docs match the template and the example")
    return 0


sys.exit(main())
