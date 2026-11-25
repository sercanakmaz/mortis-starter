"""Builds references/go/ from the Go reference solution: manifest.json, kernel-shape.txt and the pages (expanding @@go <path>@@)."""
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
VARIANT = ROOT / "_temp" / "variants-go" / "lean"
OUT = ROOT / "skills" / "mortis-scaffold" / "references" / "go"
PAGES = pathlib.Path(__file__).resolve().parent / "pages"
SKIP = {".mortis"}
AGGREGATES = ("products", "orders", "customers", "categories")

PAGE_OF = [
    ("internal/shared/", "shared-kernel.md"),
    ("internal/domain/", "domain.md"),
    ("internal/application/", "application.md"),
    ("internal/infrastructure/persistence/", "persistence.md"),
    ("internal/infrastructure/logging/", "logging.md"),
    ("internal/infrastructure/messaging/", "events-inprocess.md"),
    ("internal/testsupport/", "testing.md"),
    ("internal/api/", "hosts.md"),
    ("internal/bootstrap/", "hosts.md"),
    ("cmd/", "hosts.md"),
    (".vscode/", "hosts.md"),
]

def generic(path):
    for aggregate in AGGREGATES:
        for prefix in ("internal/domain/", "internal/application/", "internal/infrastructure/persistence/"):
            if path.startswith(prefix + aggregate + "/"):
                return path.replace(prefix + aggregate + "/", prefix + "{plural}/"), True
    return path, path.startswith("internal/application/flow_test.go") or path == "internal/application/module.go" and False

def files():
    for path in sorted(VARIANT.rglob("*")):
        relative = path.relative_to(VARIANT).as_posix()
        if path.is_file() and not (SKIP & set(path.relative_to(VARIANT).parts)):
            yield relative

def page_of(relative):
    if relative.endswith("_test.go") and not relative.startswith("internal/testsupport/"):
        return "testing.md"
    for prefix, page in PAGE_OF:
        if relative.startswith(prefix):
            return page
    return "solution.md"

def manifest():
    entries = []
    for relative in files():
        if any(relative.startswith(prefix + aggregate + "/") for prefix in ("internal/domain/", "internal/application/", "internal/infrastructure/persistence/") for aggregate in AGGREGATES):
            entries.append({"path": relative, "page": page_of(relative), "source": "snippets", "kernel": False, "when": {"example": "true"}})
            continue
        example = relative in ("internal/application/flow_test.go", "internal/testsupport/repositories.go")
        entry = {"path": relative, "page": page_of(relative), "source": "snippets" if relative.endswith(".go") else "data", "kernel": relative.startswith("internal/shared/") and not relative.endswith("_test.go")}
        if example:
            entry["when"] = {"example": "true"}
        entries.append(entry)
    return {
        "language": "go",
        "manifest_version": 1,
        "placeholders": {"Name": "the `name` row of MORTIS.md", "module": "the Go module path: the name in lower case", "plural": "an aggregate's package, plural lower case"},
        "supported": {"preset": "lean", "description": "Go is built and proved for the lean preset's values only (decision 54); any other value is refused by mortis-architect for Go."},
        "files": entries,
        "aggregate_files": {"description": "Files a project adds per aggregate.", "folders": {
            "internal/domain/{plural}": ["*.go"],
            "internal/application/{plural}": ["*.go"],
            "internal/infrastructure/persistence/{plural}": ["repository.go", "*_test.go"]}},
        "always_allowed": {"description": "Paths a project may add at any time.", "patterns": [
            "internal/infrastructure/persistence/migrations/*.sql", "docs/*.md", "docs/adr/*.md", "plans/**", ".mortis/**", "README.md", "AGENTS.md"]},
        "option_changes": {},
    }

def placeholder(text):
    return text.replace("Boilerplate", "{Name}").replace("boilerplate", "{module}")

def go_blocks(source):
    lines = source.rstrip().splitlines()
    package = lines[0].split()[1]
    index = 1
    imports = []
    while index < len(lines) and not lines[index].startswith(("func ", "type ", "const ", "var ", "//")):
        line = lines[index].strip()
        if line.startswith("import "):
            line = line[len("import "):].strip()
        if line not in ("", "(", ")"):
            imports.append(line)
        index += 1
    body = lines[index:]
    declarations, current = [], []
    for line in body:
        starts = line.startswith(("func ", "type ", "const ", "var ", "//")) and (not current or current[-1] == "")
        if starts and current and not current[-1].startswith("//") and any(l.strip() for l in current):
            declarations.append(current)
            current = []
        current.append(line)
    declarations.append(current)
    chunks, chunk = [], []
    for declaration in declarations:
        while declaration and declaration[-1] == "":
            declaration = declaration[:-1]
        pieces = [declaration]
        if len(declaration) > 58:
            pieces, piece = [], []
            for line in declaration:
                piece.append(line)
                if line == "" and len(piece) > 30:
                    pieces.append(piece)
                    piece = []
            pieces.append(piece)
        for piece in pieces:
            while piece and piece[0] == "":
                piece = piece[1:]
            while piece and piece[-1] == "":
                piece = piece[:-1]
            if not piece:
                continue
            if chunk and len(chunk) + len(piece) + 1 > 58:
                chunks.append(chunk)
                chunk = []
            chunk = chunk + ([""] if chunk else []) + piece
    if chunk:
        chunks.append(chunk)
    header = "A block that ends inside a function continues in the next one.\n\n" * any(chunk[-1] != "}" and chunk[-1] != ")" for chunk in chunks[:-1]) + f"Package `{package}`" + (", imports " + ", ".join(f"`{i}`" for i in imports) if imports else ", no imports") + (". In parts, in file order:" if len(chunks) > 1 else ":")
    return header + "\n\n" + "\n\n".join("```go\n" + "\n".join(chunk).strip("\n") + "\n```" for chunk in chunks)

def expand(text):
    def include(match):
        path = match.group(1)
        source = placeholder((VARIANT / path).read_text(encoding="utf-8"))
        if path.endswith(".go"):
            return go_blocks(source)
        language = "sql" if path.endswith(".sql") else ("json" if path.endswith(".json") else ("yaml" if path.endswith((".yml", ".yaml")) else ""))
        return f"```{language}\n{source.rstrip()}\n```"
    return re.sub(r"@@go ([^@]+)@@", include, text)

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "manifest.json").write_text(json.dumps(manifest(), indent=1) + "\n", encoding="utf-8")
    kernel = [e["path"] for e in manifest()["files"] if e["kernel"]]
    (OUT / "kernel-shape.txt").unlink(missing_ok=True)
    shape = subprocess.run([sys.executable, "-I", str(ROOT / "skills/mortis-scaffold/scripts/lang/go_shape.py"), str(VARIANT), "Boilerplate"], input="\n".join(kernel), capture_output=True, text=True).stdout
    lines = [line for line in shape.splitlines() if line]
    (OUT / "kernel-shape.txt").write_text("# Every exported declaration of the Go kernel, in order. Generated from the reference solution; verify.sh compares a project against it.\n" + "\n".join(lines) + "\n", encoding="utf-8")
    for page in sorted(PAGES.glob("*.md")):
        (OUT / page.name).write_text(expand(page.read_text(encoding="utf-8")), encoding="utf-8")
    print(f"{len(manifest()['files'])} manifest entries, {len(lines)} shape lines, {len(list(PAGES.glob('*.md')))} pages")

main()
