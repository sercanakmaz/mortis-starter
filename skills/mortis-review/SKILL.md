---
name: mortis-review
description: Reviews a change or a whole Mortis project (a project with a MORTIS.md) against its rules - runs review.sh for the mechanical checks (comments, private helpers in handlers, rules outside the aggregate, hosts naming handlers, missing registrations, writing commands without a context, tenant-less unique indexes, reactions that inject repositories, helpers in test classes, banned APIs, queue-event contracts, writes in the domain, cross-context references), then walks the judgement checklist the script cannot do, and reports findings with file, line, rule and a concrete fix, most severe first. Use whenever someone asks to review, check, audit or "look over" code, a diff, a branch or a pull request in a Mortis project, or asks whether a change follows the project's rules, even if they never name the skill.
---

# Review a Mortis change

This skill judges; it never fixes unless asked. It reads `MORTIS.md` and the project's `AGENTS.md`, runs the script, then reads the change with the checklist, and reports.

## Steps

1. **Read `MORTIS.md` and `AGENTS.md`.** The options decide which checks apply (tenant indexes only with tenancy, context references only with contexts). Without a `MORTIS.md`, say so first and review against the lean preset's rules.
2. **Run the mechanical checks.** For a change: `bash scripts/review.sh <project-dir> --diff <base>...HEAD` (or `--diff HEAD` for the working tree). For a whole project: `bash scripts/review.sh <project-dir>`. `--dry-run` lists the checks and which ones run for this project; `--format json` gives the findings as data.
3. **Walk the checklist** (`references/checklist.md`) over the changed files, layer by layer. It holds what a script cannot decide: whether a second aggregate's change is the action or a consequence (decision 28), whether a domain service needs a read port (decision 31), naming, test quality, missing places from `../mortis-develop/references/where-else.md`.
4. **Run what proves behaviour** when the change touches it and the tools are there: `dotnet build` (0 warnings), `dotnet test`, and `bash ../mortis-scaffold/scripts/verify.sh <project-dir>` for a change to options, hosts or persistence.
5. **Report** in the format of `references/findings-format.md`: errors first, then warnings, then notes; every finding with its file and line, the rule, and a fix concrete enough to apply. Then the checks that did not run and why. A clean review says so in one line and still lists what was not run.

## What counts as a finding

- A script finding is a finding unless the code shows it is a false positive; then say so and name the check, so the script can be improved.
- A checklist finding names the rule of `AGENTS.md` or the decision it comes from. "I would do it differently" is not a finding.
- A finding outside the change (old code the diff did not touch) is reported separately, as context, never mixed with the change's findings.

## References

| File | Read when |
|---|---|
| `scripts/review.sh`, `scripts/review.py` | always (the checks and their rules are listed in `review.py`) |
| `references/checklist.md` | always, after the script |
| `references/findings-format.md` | writing the report |
| `../mortis-architect/references/architecture/placement-guide.md` | a finding is about where code belongs |
| `../mortis-develop/references/where-else.md` | the change touches what an option covers |
