import json
import os
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "skills" / "mortis-scaffold" / "scripts" / "lang"))
from csharp_shape import shape_of_file  # noqa: E402

target = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "kernel-shape.txt"
manifest = json.loads((target.parent / "manifest.json").read_text())
lines = []
for entry in manifest["files"]:
    if entry["kernel"]:
        relative = entry["path"].replace("{Name}", "Boilerplate")
        overlay = HERE / "overlay" / relative
        lines += shape_of_file(overlay if overlay.exists() else ROOT / "_temp" / "template" / "assets" / "csharp" / relative, "Boilerplate")
header = [l for l in target.read_text().splitlines() if l.startswith("#")]
target.write_text("\n".join(header) + "\n" + "\n".join(lines) + "\n")
print(len(lines), "shape lines")
