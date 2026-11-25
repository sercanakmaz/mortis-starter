"""Fails when Go code outside the shared kernel creates a random id: ids come from shared.NewID (time-ordered)."""
import pathlib
import re
import sys

ROOT = pathlib.Path(sys.argv[1])
BANNED = re.compile(r"\buuid\.(New|NewString|NewRandom|Must\(uuid\.NewRandom)\(")
problems = []
for path in ROOT.rglob("*.go"):
    relative = path.relative_to(ROOT).as_posix()
    if relative.startswith(".mortis/") or relative == "internal/shared/ids.go" or relative.endswith("_test.go"):
        continue
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if BANNED.search(line):
            problems.append(f"{relative}:{number}: a random id; use shared.NewID()")
if problems:
    print("\n".join(problems))
    sys.exit(1)
