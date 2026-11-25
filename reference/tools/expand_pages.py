"""Expands page sources (reference/tools/pages/*.md) into skills/mortis-scaffold/references/csharp/*.md.

Markers, each on a line of its own:
  @@member <file>::<Type>::<name>[#n]@@   the member (with its attributes and body) as a csharp snippet
  @@decl <file>::<Type>@@                 the type's declaration line(s) up to its opening brace or semicolon
  @@data <file> [lang]@@                  the whole file (only for data files)
<file> is relative to a variant solution in reference/variants/; the first of lean, advanced, standard that has it is read, or
"variant:<preset>/<file>" reads one variant, and "extra:<build>/<file>" one of the option builds in reference/variants-extra/. "Boilerplate" becomes {Name} in the output.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "skills" / "mortis-scaffold" / "scripts" / "lang"))
from csharp_shape import mask  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
VARIANTS = ROOT / "reference" / "variants"
ORDER = ("lean", "advanced", "standard")
SOURCES = Path(__file__).resolve().parent / "pages"
TARGET = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp"


def source_file(file):
    if file.startswith("extra:"):
        return ROOT / "reference" / "variants-extra" / file[len("extra:"):]
    if file.startswith("variant:"):
        preset, _, relative = file[len("variant:"):].partition("/")
        return VARIANTS / preset / relative
    return next((VARIANTS / preset / file for preset in ORDER if (VARIANTS / preset / file).exists()), VARIANTS / ORDER[0] / file)


def read_source(file):
    return source_file(file).read_text(encoding="utf-8")


def dedent(block):
    lines = block.splitlines()
    indents = [len(l) - len(l.lstrip()) for l in lines if l.strip()]
    cut = min(indents) if indents else 0
    return "\n".join(l[cut:] for l in lines)


def find_type(text, masked, type_name):
    type_name, _, occurrence = type_name.partition("#")
    occurrence = int(occurrence or 1)
    for match in re.finditer(r"(?:class|record|interface|struct|enum)\s+" + re.escape(type_name) + r"\b", masked):
        line_start = masked.rfind("\n", 0, match.start()) + 1
        occurrence -= 1
        if occurrence == 0:
            return line_start, match
    raise SystemExit(f"type {type_name} not found")


def body_end(masked, start):
    i = start
    while i < len(masked) and masked[i] not in "{;":
        if masked.startswith("=>", i):
            j = masked.find(";", i)
            return j + 1
        i += 1
    if masked[i] == ";":
        return i + 1
    depth = 0
    while i < len(masked):
        if masked[i] == "{":
            depth += 1
        elif masked[i] == "}":
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    raise SystemExit("unbalanced")


def member(file, type_name, name, occurrence=1):
    text = read_source(file)
    masked = mask(text)
    type_start, type_match = find_type(text, masked, type_name)
    type_name = type_name.partition("#")[0]
    type_end = body_end(masked, type_match.end())
    modifiers = r"(?:(?:public|protected|private|internal|static|abstract|override|virtual|async|sealed|readonly|const|new|required)\s+)+"
    pattern = re.compile(r"^[ \t]*(?:\[[^\n]*\][ \t]*\n?[ \t]*)*(?:" + modifiers + r"[^\n;{=(]*?|(?!\s*(?:await|return|var|if|throw|foreach|for|while|using|else)\b)[\w<>\[\]][\w<>\[\],.?() ]*?\s)\b" + re.escape(name) + r"\b\s*(?:<[^>\n]*>)?\s*(?:\(|\{|=>|=|;)", re.M)
    count = 0
    for match in pattern.finditer(masked, type_start, type_end):
        line = masked[match.start():match.end()]
        if re.search(r"\b(class|record|interface)\s+" + re.escape(name) + r"\b", line) and name == type_name and "(" not in line.split(name, 1)[1][:2]:
            continue
        count += 1
        if count == occurrence:
            start = match.start()
            end = body_end(masked, match.end() - 1 if masked[match.end() - 1] in "({" else match.end() - 1)
            rest = masked[end:]
            if rest.lstrip(" \t").startswith("="):
                end = masked.find(";", end) + 1
            elif rest.lstrip(" \t").startswith(";"):
                end = masked.find(";", end) + 1
            return dedent(text[start:end]).replace("Boilerplate", "{Name}").replace("boilerplate", "{database}")
    raise SystemExit(f"member {name} not found in {type_name} ({file})")


def decl(file, type_name):
    text = read_source(file)
    masked = mask(text)
    start, match = find_type(text, masked, type_name)
    lines_before = text[:start].split("\n")[:-1]
    while lines_before and lines_before[-1].strip().startswith("["):
        start -= len(lines_before[-1]) + 1
        lines_before.pop()
    i = match.end()
    depth = 0
    while i < len(masked):
        if masked[i] == "(":
            depth += 1
        elif masked[i] == ")":
            depth -= 1
        elif masked[i] in "{;" and depth == 0:
            break
        i += 1
    snippet = text[start:i].rstrip()
    if masked[i] == ";":
        snippet += ";"
    return dedent(snippet).replace("Boilerplate", "{Name}")


def data(file):
    return read_source(file).rstrip("\n").replace("Boilerplate", "{Name}").replace("boilerplate", "{database}")


def member_names(file, type_name):
    text = read_source(file)
    masked = mask(text)
    start, match = find_type(text, masked, type_name)
    end = body_end(masked, match.end())
    body_start = masked.find("{", match.end())
    names = []
    depth = 0
    i = body_start + 1
    segment_start = i
    while i < end - 1:
        c = masked[i]
        if c in "{;" and depth == 0:
            segment = re.sub(r"\s+", " ", masked[segment_start:i]).strip()
            segment = re.sub(r"^(\[[^\]]*\]\s*)+", "", segment)
            while re.search(r"<[^<>]*>", segment):
                segment = re.sub(r"<[^<>]*>", "", segment)
            head = re.split(r"\(|=>| = |\{", segment)[0].strip()
            if head and not head.startswith(("get", "set", "init")):
                while re.search(r"<[^<>]*>", head):
                    head = re.sub(r"<[^<>]*>", "", head)
                name = head.split(" ")[-1]
                if name and name not in names:
                    names.append(name)
                elif name in names:
                    names.append(name + "#" + str(names.count(name) + 1 + sum(1 for n in names if n.startswith(name + "#"))))
            if c == "{":
                depth = 1
                i += 1
                while i < end and depth:
                    depth += masked[i] == "{"
                    depth -= masked[i] == "}"
                    i += 1
                rest = masked[i:].lstrip(" \t")
                if rest.startswith("="):
                    i = masked.find(";", i) + 1
                segment_start = i
                continue
            segment_start = i + 1
        i += 1
    return names


def split_long(lines):
    result = []
    i = 0
    while i < len(lines):
        if lines[i] == "```csharp":
            j = lines.index("```", i + 1)
            body = lines[i + 1:j]
            while len(body) > 58:
                cut = max(k for k in range(1, 56) if body[k].strip() == "")
                result += ["```csharp"] + body[:cut] + ["```", "", "The member continues:", ""]
                body = body[cut + 1:]
            result += ["```csharp"] + body + ["```"]
            i = j + 1
            continue
        result.append(lines[i])
        i += 1
    return result


def usings_for(page):
    import json
    manifest = json.loads((TARGET / "manifest.json").read_text(encoding="utf-8"))
    rows = []
    for entry in manifest["files"]:
        if entry["page"] != page or not entry["path"].endswith(".cs") or entry.get("glob"):
            continue
        relative = entry["path"].replace("{Name}", "Boilerplate")
        if not source_file(relative).exists():
            relative = next((f"extra:{build.name}/{relative}" for build in sorted((ROOT / "reference" / "variants-extra").glob("*")) if (build / relative).exists()), None)
            if relative is None:
                continue
        source = read_source(relative)
        usings = [l.strip()[6:-1] for l in source.splitlines() if l.startswith("using ") and not l.startswith("using var ")]
        namespace = next((l.strip()[10:].rstrip(";").strip() for l in source.splitlines() if l.startswith("namespace ")), "")
        rows.append((entry["path"], namespace.replace("Boilerplate", "{Name}"), ", ".join(sorted(u.replace("Boilerplate", "{Name}") for u in usings)) or "none"))
    out = ["| File | Namespace | Usings |", "|---|---|---|"]
    out += [f"| `{p}` | `{n}` | {u} |" for p, n, u in rows]
    return out


def expand(source):
    out = []
    for line in source.splitlines():
        usings = re.fullmatch(r"@@usings ([^@]+)@@", line.strip())
        if usings:
            out += usings_for(usings.group(1).strip())
            continue
        many = re.fullmatch(r"@@members ([^@]+)@@", line.strip())
        if many:
            file, type_name = many.group(1).strip().split("::")
            declaration = ["```csharp", decl(file, type_name), "```"]
            emitted = [line for line in out if line.strip()][-3:]
            if emitted != declaration:
                out += declaration
            for name in member_names(file, type_name):
                base, _, occ = name.partition("#")
                out += ["", "```csharp", member(file, type_name, base, int(occ or 1)), "```"]
            continue
        m = re.fullmatch(r"@@(member|decl|data) ([^@]+)@@", line.strip())
        if not m:
            out.append(line)
            continue
        kind, arg = m.group(1), m.group(2).strip()
        if kind == "data":
            parts = arg.split()
            lang = parts[1] if len(parts) > 1 else ""
            out += [f"```{lang}", data(parts[0]), "```"]
            continue
        bits = arg.split("::")
        if kind == "decl":
            out += ["```csharp", decl(bits[0], bits[1]), "```"]
        else:
            name, _, occ = bits[2].partition("#")
            out += ["```csharp", member(bits[0], bits[1], name, int(occ or 1)), "```"]
    out = split_long("\n".join(out).split("\n"))
    return "\n".join(out) + "\n"


def main():
    names = sys.argv[1:] or [p.name for p in sorted(SOURCES.glob("*.md"))]
    for name in names:
        (TARGET / name).write_text(expand((SOURCES / name).read_text(encoding="utf-8")), encoding="utf-8")
        print("wrote", name)


if __name__ == "__main__":
    main()
