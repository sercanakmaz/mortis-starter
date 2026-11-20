import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ARCH = ROOT / "skills" / "mortis-architect" / "references" / "architecture"
TEMPLATE_SHARED = ROOT / "_temp" / "variants" / "lean" / "src" / "Boilerplate.Shared"
TYPE_MAP = ROOT / "tests" / "fixtures" / "shared-type-map.json"
QUESTIONS = ROOT / "tests" / "fixtures" / "placement-questions.json"

FILES = ["layers.md", "building-blocks.md", "request-lifecycle.md", "events.md",
         "placement-guide.md", "scenarios.md", "pseudo-code.md", "glossary.md", "example-domain.md"]

BLOCKS = ["Entity", "AggregateRoot", "Child entity", "Value object", "Domain event",
          "Repository interface", "Domain service", "Command", "Validator", "Command handler",
          "Command pipeline", "Dispatcher", "DTO", "Sync event handler", "Async event handler",
          "Unit of work", "Event publisher", "Host adapter", "Anti-corruption layer", "Failure"]

LANGUAGE_TERMS = [
    r"\bTask\b", r"\basync\s+(def|function|func|fn)\b", r"\bawait\s+\S+\(", r"\bIServiceCollection\b",
    r"\bDbContext\b", r"\bgoroutine\b", r"\bchan\b", r"interface\{\}", r"\bstruct\b",
    r"\bnamespace\b", r"^using\s", r"\[Fact\]", r"\[Theory\]", r"\bIEnumerable\b", r"\bnil\b",
    r"\bnull\b", r"\bpublic\s+(class|record|void)\b", r"\bfunc\s+\w+\(", r"\.cs\b", r"\.go\b",
]

LAYER_TAG = re.compile(r"^\d+\.\s+\[(Host|Application|Domain|Infrastructure|Shared)(\s*/\s*(Host|Application|Domain|Infrastructure|Shared))*\]")

errors = []


def read(name):
    path = ARCH / name
    if not path.exists():
        errors.append(f"missing file: {name}")
        return ""
    return path.read_text(encoding="utf-8")


texts = {name: read(name) for name in FILES}

for name, text in texts.items():
    for number, line in enumerate(text.splitlines(), 1):
        for pattern in LANGUAGE_TERMS:
            if re.search(pattern, line):
                errors.append(f"{name}:{number}: language-specific term {pattern!r}: {line.strip()[:80]}")

headings = set(re.findall(r"^## (.+)$", texts["building-blocks.md"], re.M))
for block in BLOCKS:
    if block not in headings:
        errors.append(f"building-blocks.md: no section for {block!r}")

type_map = json.loads(TYPE_MAP.read_text(encoding="utf-8"))
for target in set(type_map.values()):
    if target not in headings:
        errors.append(f"shared-type-map.json: {target!r} is not a section of building-blocks.md")
if TEMPLATE_SHARED.exists():
    for path in sorted(TEMPLATE_SHARED.rglob("*.cs")):
        if {"obj", "bin"} & set(path.relative_to(TEMPLATE_SHARED).parts):
            continue
        key = path.relative_to(TEMPLATE_SHARED).as_posix()
        if key not in type_map:
            errors.append(f"Shared type without a building block: {key}")
    for key in type_map:
        if not (TEMPLATE_SHARED / key).exists():
            errors.append(f"shared-type-map.json names a file that does not exist: {key}")

scenario_parts = re.split(r"^## ", texts["scenarios.md"], flags=re.M)[1:]
if len(scenario_parts) < 8:
    errors.append(f"scenarios.md: {len(scenario_parts)} scenarios, at least 8 required")
for part in scenario_parts:
    title = part.splitlines()[0]
    steps = [l for l in part.splitlines()[1:] if re.match(r"^\d+\.\s", l)]
    if not steps:
        errors.append(f"scenarios.md: {title!r} has no numbered steps")
    for step in steps:
        if not LAYER_TAG.match(step):
            errors.append(f"scenarios.md: {title!r}: step without a layer tag: {step[:70]}")
    if "**Wrong placement:**" not in part:
        errors.append(f"scenarios.md: {title!r} has no wrong placement")

guide = texts["placement-guide.md"]
for heading in ("## How they work together", "## Decision cheat sheet", "## Common pitfalls", "## The lookup table"):
    if heading not in guide:
        errors.append(f"placement-guide.md: missing section {heading!r}")
table = guide.split("| I need to", 1)[-1].split("\n## ", 1)[0]
rows = [l for l in table.splitlines() if l.startswith("| ") and not l.startswith("|---")]
if len(rows) < 40:
    errors.append(f"placement-guide.md: {len(rows)} rows in the lookup table, at least 40 required")
cheat = guide.split("## Decision cheat sheet", 1)[-1].split("\n## ", 1)[0]
if len(re.findall(r"^\d+\.\s", cheat, re.M)) < 6:
    errors.append("placement-guide.md: the decision cheat sheet needs at least 6 ordered questions")
if "## How many of each" not in texts["events.md"]:
    errors.append("events.md: missing the section 'How many of each' (one command can raise N events)")

questions = json.loads(QUESTIONS.read_text(encoding="utf-8"))
if len(questions) != 20:
    errors.append(f"placement-questions.json: {len(questions)} questions, 20 required")
for item in questions:
    if item["block"] not in BLOCKS and item["block"] != "Aggregate operation":
        errors.append(f"placement-questions.json: unknown block {item['block']!r}")

if errors:
    print("\n".join(errors))
    print(f"\nFAILED: {len(errors)} problems")
    sys.exit(1)
print("architecture layer: ok")
