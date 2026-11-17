#!/usr/bin/env bash
# Fails when anything under skills/ is project source, when a snippet breaks the snippet rules,
# or when a document outside _sources/ names a codebase that was studied.
set -uo pipefail
REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SKILLS="${CHECK_SKILLS_DIR:-$REPO_DIR/skills}"
PROBLEMS=0
problem() { PROBLEMS=$((PROBLEMS + 1)); echo "INSTALLABLE $1"; }

while IFS= read -r file; do
  problem "project source under skills/: ${file#$REPO_DIR/}"
done < <(find "$SKILLS" -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.sln' -o -name '*.slnx' -o -name '*.go' -o -name 'go.mod' -o -name '*.props' \))

SNIPPETS="$(python3 -I - "$SKILLS" <<'PY'
import re, sys, pathlib
for path in sorted(pathlib.Path(sys.argv[1]).rglob("*.md")):
    text = path.read_text(encoding="utf-8")
    for match in re.finditer(r"```(csharp|go)\n(.*?)```", text, re.S):
        body = match.group(2)
        line = text[:match.start()].count("\n") + 1
        if body.count("\n") > 60:
            print(f"{path}:{line}: a snippet longer than sixty lines")
        if re.search(r"^\s*(namespace|package)\s", body, re.M) and re.search(r"\b(class|record|interface|struct|type)\s+\w+", body):
            print(f"{path}:{line}: a snippet holds a namespace or package and a complete type")
PY
)"
[ -n "$SNIPPETS" ] && while IFS= read -r line; do problem "${line#$REPO_DIR/}"; done <<<"$SNIPPETS"

NAMES="$REPO_DIR/_sources/names.txt"
if [ -f "$NAMES" ]; then
  PATTERN="$(grep -v '^\s*$' "$NAMES" | grep -v '^#' | paste -sd'|' -)"
  if [ -n "$PATTERN" ]; then
    while IFS= read -r file; do
      problem "an origin is named in ${file#$REPO_DIR/}"
    done < <(grep -rliIE --exclude-dir=__pycache__ "$PATTERN" "$SKILLS" "$REPO_DIR/docs" "$REPO_DIR/plans" "$REPO_DIR/README.md" "$REPO_DIR/CHANGELOG.md" "$REPO_DIR/tests" 2>/dev/null | grep -v "/_sources/")
  fi
else
  echo "note: _sources/names.txt is absent; the origin check did not run"
fi

if [ "$PROBLEMS" -eq 0 ]; then echo "skills are installable: no project source, snippets within the rules, no origins"; exit 0; fi
echo "$PROBLEMS problem(s)"; exit 1
