import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(ROOT / "skills" / "mortis-scaffold" / "scripts" / "lang"))
from csharp_shape import shape_of_file  # noqa: E402

VARIANTS = ROOT / "reference" / "variants"
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


POSTGRES_MEMBERS = ("DesignTimeConnectionString", "RowVersionColumn")
ADDON_MEMBERS = {"InvalidRequestCode": "api-conventions", "InvalidRequestMessage": "api-conventions", "RouteNotFoundCode": "api-conventions", "RouteNotFoundMessage": "api-conventions", "MethodNotAllowedCode": "api-conventions", "MethodNotAllowedMessage": "api-conventions", "TraceId": "observability", "SpanId": "observability", "Chain": "loop-guard"}
MEMBER_CONDITIONS = {"RebuildOrderSummaryCommand": "queries=read-models"}


def condition(relative, line, presets):
    for member, addon in ADDON_MEMBERS.items():
        if f" {member} " in line:
            return f"[addons={addon}] "
    for member, gate in MEMBER_CONDITIONS.items():
        if f" {member} " in line:
            return f"[{gate}] "
    specific = [k for member, k in KEY_OF_MEMBER.items() if member in line][:1] + [k for part, k in KEY_OF_FILE.items() if part in relative]
    for keys in (specific, specific + ["events.outbox", "exceptions", "auth", "tenancy.mode", "languages.mode"]):
        for combination in [[key] for key in keys] + [[first, second] for first in keys for second in keys if first < second]:
            matching = {preset for preset in PRESETS if all(PRESETS[preset][k] in {PRESETS[p][k] for p in presets} for k in combination)}
            if matching == presets:
                return "[" + ",".join(f"{k}={'|'.join(dict.fromkeys(v for p in PRESETS if p in presets for v in ALSO.get((k, PRESETS[p][k]), [PRESETS[p][k]])))}" for k in combination) + "] "
    raise AssertionError(f"no option explains {line} in {relative}")


target = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "kernel-shape.txt"
manifest = json.loads((target.parent / "manifest.json").read_text())
sys.path.insert(0, str(ROOT / "skills" / "mortis-scaffold" / "scripts"))
from decisions import load  # noqa: E402
from structure import applies  # noqa: E402

VALUES = {preset: load(str(VARIANTS / preset / "MORTIS.md"))[2] for preset in PRESETS}
GATES = ("queries", "testing", "addons")


def gate_of(entry):
    when = {key: value for key, value in (entry.get("when") or (entry.get("when_any") or [{}])[0]).items() if key in GATES}
    return ",".join(f"{key}={'|'.join(value) if isinstance(value, list) else value}" for key, value in when.items())


lines = []
for entry in manifest["files"]:
    if not entry["kernel"] or entry.get("unbuilt"):
        continue
    relative = entry["path"].replace("{Name}", "Boilerplate")
    database = entry.get("when", {}).get("database")
    gate = gate_of(entry)
    applicable = [preset for preset in PRESETS if (VARIANTS / preset / relative).exists() and (not gate or applies(entry, VALUES[preset]))]
    by_preset = {preset: shape_of_file(VARIANTS / preset / relative, "Boilerplate") for preset in applicable}
    for line, presets in merged(by_preset):
        marked = line if len(presets) == len(applicable) else condition(relative, line, presets) + line
        if gate:
            marked = f"[{gate}," + marked[1:] if marked.startswith("[") else f"[{gate}] " + marked
        if database:
            marked = f"[database={database}," + marked[1:] if marked.startswith("[") else f"[database={database}] " + marked
        elif any(f" {member} " in line for member in POSTGRES_MEMBERS):
            marked = "[database=postgres," + marked[1:] if marked.startswith("[") else "[database=postgres] " + marked
        lines.append(marked)
for extra in sorted(Path(__file__).parent.glob("shape-*.txt")):
    lines += [line for line in extra.read_text().splitlines() if line and not line.startswith("#")]
header = [l for l in target.read_text().splitlines() if l.startswith("#")]
target.write_text("\n".join(header) + "\n" + "\n".join(lines) + "\n")
print(len(lines), "shape lines")
