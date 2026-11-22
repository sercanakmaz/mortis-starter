"""Runs mortis-review's review.py on the variants (no finding expected) and on each fixture in tests/fixtures/review/: a copy of a
variant with one planted violation, which must be reported and nothing else."""
import json
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / "skills" / "mortis-review" / "scripts" / "review.py"
VARIANTS = ROOT / "_temp" / "variants"
failures = []


def run(project, *extra):
    result = subprocess.run([sys.executable, "-I", str(REVIEW), str(project), "--format", "json", *extra], capture_output=True, text=True)
    return json.loads(result.stdout)["findings"]


def apply(project, edits):
    for edit in edits:
        path = project / edit["file"]
        if "write" in edit:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(edit["write"])
            continue
        text = path.read_text()
        if edit["find"] not in text:
            raise ValueError(f"{edit['file']}: the text to replace is not there: {edit['find'][:60]!r}")
        path.write_text(text.replace(edit["find"], edit["replace"], 1))


def git(project, *args):
    subprocess.run(["git", "-C", str(project), "-c", "user.name=fixture", "-c", "user.email=fixture@example.com", *args], check=True, capture_output=True)


if VARIANTS.is_dir():
    for preset in ("lean", "standard", "advanced"):
        findings = run(VARIANTS / preset)
        if findings:
            failures.append(f"variant {preset}: expected no finding, got {[(f['check'], f['file'], f['line']) for f in findings][:5]}")

for fixture in sorted((ROOT / "tests" / "fixtures" / "review").glob("*.json")):
    case = json.loads(fixture.read_text())
    with tempfile.TemporaryDirectory() as temporary:
        project = Path(temporary) / "project"
        shutil.copytree(VARIANTS / case["base"], project, ignore=shutil.ignore_patterns("bin", "obj", ".mortis"))
        for key, value in case.get("mortis", {}).items():
            mortis = project / "MORTIS.md"
            mortis.write_text(re.sub(rf"^\| {re.escape(key)} \|[^|]*\|", f"| {key} | {value} |", mortis.read_text(), flags=re.M))
        extra = []
        if case.get("git"):
            apply(project, case.get("before", []))
            git(project, "init", "-q")
            git(project, "add", "-A")
            git(project, "commit", "-q", "-m", "base")
            extra = ["--diff", "HEAD"]
        try:
            apply(project, case["edits"])
        except ValueError as error:
            failures.append(f"{fixture.name}: {error}")
            continue
        found = sorted({(f["check"], f["file"]) for f in run(project, *extra)})
        expected = sorted(tuple(e) for e in case["expect"])
        if found != expected:
            failures.append(f"{fixture.name}: expected {expected}, got {found}")

for failure in failures:
    print("REVIEW", failure)
print("review fixtures: ok" if not failures else f"{len(failures)} problem(s)")
sys.exit(1 if failures else 0)
