import re
import sys
from pathlib import Path

TYPE_RE = re.compile(r"^(?P<mods>(?:(?:public|internal|protected|private|static|abstract|sealed|partial|readonly|file)\s+)*)(?P<kind>class|record|interface|struct|enum)\s+(?P<name>\w+)")
SKIP_DIRS = {"bin", "obj", "Migrations", ".git"}


def mask(text):
    out = list(text)
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if text.startswith("//", i):
            j = text.find("\n", i)
            j = n if j < 0 else j
            for k in range(i, j):
                out[k] = " "
            i = j
            continue
        if text.startswith("/*", i):
            j = text.find("*/", i)
            j = n if j < 0 else j + 2
            for k in range(i, j):
                if out[k] != "\n":
                    out[k] = " "
            i = j
            continue
        if c == "'":
            j = i + 1
            while j < n and text[j] != "'":
                j += 2 if text[j] == "\\" else 1
            for k in range(i + 1, min(j, n)):
                out[k] = " "
            i = j + 1
            continue
        if c == '"':
            verbatim = i > 0 and text[i - 1] == "@" or (i > 1 and text[i - 2:i] in ("$@", "@$"))
            j = i + 1
            while j < n:
                if verbatim and text[j] == '"' and j + 1 < n and text[j + 1] == '"':
                    j += 2
                    continue
                if not verbatim and text[j] == "\\":
                    j += 2
                    continue
                if text[j] == '"':
                    break
                j += 1
            for k in range(i + 1, min(j, n)):
                if out[k] != "\n":
                    out[k] = " " if text[k] in "{};" else text[k]
            i = j + 1
            continue
        i += 1
    return "".join(out)


def clean(segment):
    segment = re.sub(r"\s+", " ", segment).strip()
    while segment.startswith("["):
        depth = 0
        for index, char in enumerate(segment):
            depth += char == "["
            depth -= char == "]"
            if depth == 0:
                segment = segment[index + 1:].strip()
                break
        else:
            break
    segment = re.sub(r"\(\s+", "(", segment)
    segment = re.sub(r"\s+\)", ")", segment)
    return segment


def shape_of_file(path, name_token=None):
    text = Path(path).read_text(encoding="utf-8")
    masked = mask(text)
    lines = []
    namespace = ""
    stack = []
    depth = 0
    start = 0
    skip_until = None
    pending_property = None
    i = 0
    n = len(masked)
    while i < n:
        c = masked[i]
        if c not in "{};":
            i += 1
            continue
        segment = clean(text[start:i])
        raw = clean(masked[start:i])
        start = i + 1
        if skip_until is not None:
            if c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
                if depth == skip_until:
                    skip_until = None
            i += 1
            continue
        if pending_property is not None:
            if c == "}":
                depth -= 1
                if depth == pending_property[1]:
                    member, _, accessors = pending_property
                    lines.append(f"{member} {{ {' '.join(a + ';' for a in accessors)} }}")
                    pending_property = None
            elif c == ";":
                if raw:
                    pending_property[2].append(raw)
            elif c == "{":
                depth += 1
                skip_until = depth - 1
                pending_property[2].append(raw)
            i += 1
            continue
        if c == "}":
            depth -= 1
            if stack and stack[-1][1] == depth:
                stack.pop()
            i += 1
            continue
        if raw.startswith("namespace "):
            namespace = raw[len("namespace "):].strip()
            if c == "{":
                depth += 1
            i += 1
            continue
        if raw.startswith("using ") or not raw or raw.startswith("="):
            if c == "{":
                depth += 1
                skip_until = depth - 1
            i += 1
            continue
        match = TYPE_RE.match(raw)
        in_type = stack and stack[-1][1] == depth - 0 and True
        current = stack[-1] if stack and depth == stack[-1][1] + 1 else None
        if match:
            visible = "public" in match.group("mods").split() or (current and current[2] == "interface")
            if visible:
                lines.append(f"type {namespace}: {segment}")
            if c == "{":
                depth += 1
                stack.append((match.group("name"), depth - 1, match.group("kind"), visible))
            i += 1
            continue
        if current is None:
            if c == "{":
                depth += 1
                skip_until = depth - 1
            i += 1
            continue
        type_name, _, kind, visible = current
        words = raw.split(" ")
        is_public = words[0] in ("public", "protected") or kind == "interface"
        is_public = is_public and not raw.startswith("protected private") and visible
        if kind == "enum":
            if c == "}" or c == ";" or c == "{":
                pass
            i += 1
            continue
        member = None
        if is_public:
            cut = raw.split(" => ", 1)[0]
            member = f"member {namespace}.{type_name}: {segment[:len(cut)]}"
        if c == "{":
            depth += 1
            if member and "(" not in raw.split(" => ", 1)[0].split(" where ", 1)[0] and " => " not in raw:
                pending_property = (member, depth - 1, [])
            else:
                if member:
                    lines.append(member)
                skip_until = depth - 1
        elif c == ";":
            if member:
                lines.append(member)
        i += 1
    if name_token:
        database = name_token.lower().replace(".", "")
        lines = [line.replace(name_token, "{Name}").replace(database, "{database}") for line in lines]
    return lines


def shape(root, files, name_token=None):
    result = []
    for relative in files:
        path = Path(root) / relative
        if path.exists():
            result.extend(shape_of_file(path, name_token))
    return result


def main(argv):
    if len(argv) < 2:
        print("usage: csharp_shape.py <root> [name-token] < list-of-relative-files", file=sys.stderr)
        return 2
    root = argv[1]
    token = argv[2] if len(argv) > 2 else None
    files = [line.strip() for line in sys.stdin if line.strip()]
    for line in shape(root, files, token):
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
