"""Prints the lines of kernel-shape.txt that apply to one project.

A line "[key=v1|v2] text" applies when the project's MORTIS.md gives key one of those values, and "[k1=v1,k2=v2|v3] text" when every
key matches; for a list key (addons) it applies when the list holds one of the values. Other lines always apply.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from decisions import load  # noqa: E402


def matches(value, options):
    if isinstance(value, list):
        return any(item in options for item in value)
    return str(value) in options


def main(shape_path, decisions_path):
    _, _, values = load(decisions_path)
    for line in Path(shape_path).read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        condition = re.match(r"\[([\w.]+=[^\]]+)\] ", line)
        if condition is None:
            print(line)
        elif all(matches(values.get(key), options.split("|")) for key, options in (part.split("=", 1) for part in condition.group(1).split(","))):
            print(line[condition.end():])


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
