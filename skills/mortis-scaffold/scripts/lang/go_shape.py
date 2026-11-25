"""Prints, from the kernel files named on stdin, every exported declaration with its signature, one per line; verify.sh compares the output with kernel-shape.txt."""
import pathlib
import re
import sys

ROOT = pathlib.Path(sys.argv[1])
NAME = sys.argv[2]

def shape(path):
    relative = path.relative_to(ROOT).as_posix()
    lines = []
    text = path.read_text(encoding="utf-8")
    for line in text.splitlines():
        match = re.match(r"^func (\([^)]*\) )?([A-Z]\w*)(\[[^\]]*\])?\((.*)$", line)
        if match:
            lines.append(f"{relative}: {line.rstrip(' {')}")
            continue
        match = re.match(r"^type ([A-Z]\w*)(\[[^\]]*\])? (struct|interface|func|\w)", line)
        if match:
            lines.append(f"{relative}: type {match.group(1)}{match.group(2) or ''} {match.group(3)}")
            continue
        match = re.match(r"^\t([A-Z]\w*)\s+(\S.*)$", line)
        if match and not line.strip().endswith("{"):
            lines.append(f"{relative}:   {match.group(1)} {match.group(2).split('//')[0].strip()}")
    return lines

actual = []
for name in sys.stdin.read().split():
    path = ROOT / name
    if path.exists():
        actual.extend(shape(path))

print("\n".join(actual))
