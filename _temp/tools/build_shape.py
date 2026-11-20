import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "skills" / "mortis-scaffold" / "scripts" / "lang"))
from csharp_shape import shape_of_file  # noqa: E402

VARIANTS = ROOT / "_temp" / "variants"
PRESETS = {"lean": {"events.outbox": "none", "exceptions": "minimal"},
           "standard": {"events.outbox": "after-command", "exceptions": "coded"},
           "advanced": {"events.outbox": "background", "exceptions": "catalogued"}}
KEY_OF_MEMBER = {"SaveChangesAsync": "exceptions"}

target = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "kernel-shape.txt"
manifest = json.loads((target.parent / "manifest.json").read_text())
lines = []
for entry in manifest["files"]:
    if not entry["kernel"]:
        continue
    relative = entry["path"].replace("{Name}", "Boilerplate")
    by_preset = {preset: shape_of_file(VARIANTS / preset / relative, "Boilerplate") for preset in PRESETS}
    assert len({len(shape) for shape in by_preset.values()}) == 1, relative
    for position, line in enumerate(by_preset["lean"]):
        values = {preset: shape[position] for preset, shape in by_preset.items()}
        if len(set(values.values())) == 1:
            lines.append(line)
            continue
        key = next((k for member, k in KEY_OF_MEMBER.items() if member in line), "events.outbox")
        for text in dict.fromkeys(values.values()):
            options = "|".join(PRESETS[preset][key] for preset, value in values.items() if value == text)
            lines.append(f"[{key}={options}] {text}")
header = [l for l in target.read_text().splitlines() if l.startswith("#")]
target.write_text("\n".join(header) + "\n" + "\n".join(lines) + "\n")
print(len(lines), "shape lines")
