"""The house rules of a generated Go project that the compiler and gofmt do not check."""
import pathlib
import re
import sys

ROOT = pathlib.Path(sys.argv[1])
problems = []
DECLARATION = re.compile(r"^(func|type|const|var|package)\b|^\t?[A-Z]\w*\s|^\t?\)")

for path in sorted(ROOT.rglob("*.go")):
    relative = path.relative_to(ROOT).as_posix()
    if relative.startswith(".mortis/"):
        continue
    lines = path.read_text(encoding="utf-8").splitlines()
    depth = 0
    for number, line in enumerate(lines, 1):
        stripped = line.strip()
        if stripped.startswith("//go:"):
            continue
        if "//" in line and not re.search(r'"[^"]*//[^"]*"|`[^`]*//', line):
            comment_at = line.index("//")
            before = line[:comment_at].strip()
            following = lines[number] if number < len(lines) else ""
            if before or depth > 0 or not DECLARATION.search(following.lstrip() if following.startswith("\t") else following):
                problems.append(f"{relative}:{number}: a comment that is not a doc comment on a declaration (house rule: no comments)")
        if re.match(r"^func init\(\)", line):
            problems.append(f"{relative}:{number}: an init function; wire explicitly in cmd/*")
        if re.search(r"\bpanic\(", line) and not relative.startswith("cmd/") and not relative.endswith("_test.go"):
            problems.append(f"{relative}:{number}: panic outside a host; return an error")
        if re.search(r"\bfmt\.Print", line) and not relative.startswith("cmd/") and not relative.endswith("_test.go"):
            problems.append(f"{relative}:{number}: printing outside a host; log through slog")
        depth += line.count("{") - line.count("}")
    if relative.endswith("_test.go"):
        for number, line in enumerate(lines, 1):
            match = re.match(r"^func (Test\w+)\(", line)
            if match and not re.match(r"^Test[A-Z]\w*(_\w+)*$", match.group(1)):
                problems.append(f"{relative}:{number}: a test name that does not read Test<Unit>_<Situation>_<Outcome> or Test<Unit>")

if problems:
    print("\n".join(problems))
    sys.exit(1)
