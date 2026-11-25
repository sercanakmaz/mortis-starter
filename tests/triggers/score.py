"""Scores a trigger run: per mortis skill, the share of its should-trigger prompts that chose it and of other prompts that chose it.
Usage: score.py cases.json run.json [run.json ...]"""
import json
import sys

cases = json.load(open(sys.argv[1]))
for path in sys.argv[2:]:
    run = {int(k): v for k, v in json.load(open(path)).items()}
    print(f"== {path}")
    for skill in ["architect", "scaffold", "develop", "review"]:
        name = "mortis-" + skill
        should = [c for c in cases if c["expected"] == skill]
        should_not = [c for c in cases if c["expected"] != skill]
        hit = sum(run.get(c["id"]) == name for c in should)
        false = [c["id"] for c in should_not if run.get(c["id"]) == name]
        missed = [(c["id"], run.get(c["id"])) for c in should if run.get(c["id"]) != name]
        print(f"{name}: triggers {hit}/{len(should)} ({100 * hit // len(should)}%), false {len(false)}/{len(should_not)} ({100 * len(false) // len(should_not)}%) missed {missed} false {false}")
