import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "skills" / "mortis-scaffold" / "scripts" / "lang"))
from csharp_shape import shape_of_file  # noqa: E402

VARIANTS = ROOT / "_temp" / "variants"
PRESETS = {"lean": {"events.outbox": "none", "exceptions": "minimal", "auth": "none", "tenancy.mode": "none", "languages.mode": "none"},
           "standard": {"events.outbox": "after-command", "exceptions": "coded", "auth": "jwt", "tenancy.mode": "none", "languages.mode": "none"},
           "advanced": {"events.outbox": "background", "exceptions": "catalogued", "auth": "jwt", "tenancy.mode": "interface", "languages.mode": "data"}}
ALSO = {("languages.mode", "data"): ["messages", "data"]}
KEY_OF_MEMBER = {"SaveChangesAsync(bool": "tenancy.mode", "SaveChangesAsync": "exceptions"}
KEY_OF_FILE = {"Console/": "auth", "Aggregates/": "tenancy.mode", "AppDbContext": "tenancy.mode", "PersistenceConstants": "tenancy.mode", "ContextCommandFilter": "auth", "Filters/ContextCommandFilter": "tenancy.mode", "Exceptions/": "languages.mode", "ExceptionHandler": "languages.mode"}


def merged(shapes):
    out = []
    for preset, shape in shapes.items():
        position = 0
        for line in shape:
            found = next((i for i in range(position, len(out)) if out[i][0] == line and preset not in out[i][1]), None)
            if found is None:
                out.insert(position, (line, {preset}))
                position += 1
            else:
                out[found][1].add(preset)
                position = found + 1
    return out


def condition(relative, line, presets):
    specific = [k for member, k in KEY_OF_MEMBER.items() if member in line][:1] + [k for part, k in KEY_OF_FILE.items() if part in relative]
    for keys in (specific, specific + ["events.outbox", "exceptions", "auth", "tenancy.mode", "languages.mode"]):
        for combination in [[key] for key in keys] + [[first, second] for first in keys for second in keys if first < second]:
            matching = {preset for preset in PRESETS if all(PRESETS[preset][k] in {PRESETS[p][k] for p in presets} for k in combination)}
            if matching == presets:
                return "[" + ",".join(f"{k}={'|'.join(dict.fromkeys(v for p in PRESETS if p in presets for v in ALSO.get((k, PRESETS[p][k]), [PRESETS[p][k]])))}" for k in combination) + "] "
    raise AssertionError(f"no option explains {line} in {relative}")


target = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "kernel-shape.txt"
manifest = json.loads((target.parent / "manifest.json").read_text())
lines = []
for entry in manifest["files"]:
    if not entry["kernel"] or entry.get("source") == "snippets" and entry.get("when", {}).get("database") == "mongo":
        continue
    relative = entry["path"].replace("{Name}", "Boilerplate")
    database = entry.get("when", {}).get("database")
    by_preset = {preset: shape_of_file(VARIANTS / preset / relative, "Boilerplate") for preset in PRESETS}
    for line, presets in merged(by_preset):
        marked = line if len(presets) == len(PRESETS) else condition(relative, line, presets) + line
        if database:
            marked = f"[database={database}," + marked[1:] if marked.startswith("[") else f"[database={database}] " + marked
        lines.append(marked)
lines += [line for line in (Path(__file__).parent / "shape-mongo.txt").read_text().splitlines() if line and not line.startswith("#")]
header = [l for l in target.read_text().splitlines() if l.startswith("#")]
target.write_text("\n".join(header) + "\n" + "\n".join(lines) + "\n")
print(len(lines), "shape lines")
