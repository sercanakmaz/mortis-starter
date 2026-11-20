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
VARIANTS = HERE / "overlay-variants"
MODES = {"none": [], "background": ["background", "outbox"], "after-command": ["after-command", "outbox"]}
lines = []
for entry in manifest["files"]:
    if entry["kernel"]:
        relative = entry["path"].replace("{Name}", "Boilerplate")
        overlay = HERE / "overlay" / relative
        base = overlay if overlay.exists() else ROOT / "_temp" / "template" / "assets" / "csharp" / relative
        by_mode = {}
        for mode, folders in MODES.items():
            source = next((VARIANTS / folder / relative for folder in folders if (VARIANTS / folder / relative).exists()), base)
            by_mode[mode] = shape_of_file(source, "Boilerplate")
        if all(shape == by_mode["none"] for shape in by_mode.values()):
            lines += by_mode["none"]
            continue
        assert len({len(shape) for shape in by_mode.values()}) == 1, relative
        for position, line in enumerate(by_mode["none"]):
            if all(shape[position] == line for shape in by_mode.values()):
                lines.append(line)
            else:
                lines += [f"[events.outbox={mode}] {shape[position]}" for mode, shape in by_mode.items()]
header = [l for l in target.read_text().splitlines() if l.startswith("#")]
target.write_text("\n".join(header) + "\n" + "\n".join(lines) + "\n")
print(len(lines), "shape lines")
