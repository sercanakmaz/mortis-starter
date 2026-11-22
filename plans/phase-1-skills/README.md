# Phase 1 — Build the skills

Everything the skills know is written in this phase. **No C# is generated, compiled or run**, and the existing C# solution in `_temp/template/` is not changed. That is deliberate: generation is saved for phase 2, when it can exercise everything at once.

## What "done" means in this phase

A step is done when its pages, entries and checks exist, `tests/check_docs.py` and `tests/check_installable.sh` pass, the Python unit tests of the validator and parser pass, and every page and check the step wrote is listed in `docs/unverified.md`. Nothing more can be claimed: a snippet that has not been compiled is not known to be right.

Cheap checks that do run in this phase (no generation, no build):
- document checks: names, links, the snippet rules, one-way wording, no origins;
- the decisions-file parser and validator tests;
- the layer 1 reader test (step 01) and the interview evals (step 02), which use a model but generate no project;
- a syntax check of the shell and Python scripts.

## Steps

| # | File | Needs | Size |
|---|---|---|---|
| 01 | [01-architecture-layer.md](01-architecture-layer.md) | none | medium |
| 02 | [02-decision-model.md](02-decision-model.md) | 01 | medium |
| 03 | [03-scaffold-foundations.md](03-scaffold-foundations.md) | 02 | large |
| 04 | [04-command-context.md](04-command-context.md) | 03 | medium |
| 05 | [05-keys.md](05-keys.md) | 03 | small |
| 06 | [06-correlation.md](06-correlation.md) | 04 | small |
| 07 | [07-exception-levels.md](07-exception-levels.md) | 03 | small |
| 08 | [08-outbox.md](08-outbox.md) | 04, 06 | large |
| 09 | [09-rabbitmq-bus.md](09-rabbitmq-bus.md) | 08 | large |
| 10 | [10-testing-levels.md](10-testing-levels.md) | 08 | medium (written after step 16, decision 49) |
| 11 | [11-multi-tenancy.md](11-multi-tenancy.md) | 04, 08 | medium |
| 12 | [12-multi-language.md](12-multi-language.md) | 04, 07 | medium |
| 13 | [13-mongodb.md](13-mongodb.md) | 08, 11 | large |
| 14 | [14-queries.md](14-queries.md) | 03 | medium |
| 15 | [15-authentication.md](15-authentication.md) | 04 | medium |
| 16 | [16-api-and-observability.md](16-api-and-observability.md) | 06 | medium |
| 17 | [17-develop-and-review-skills.md](17-develop-and-review-skills.md) | 03 to 16 | large |
| 18 | [18-advanced-addons.md](18-advanced-addons.md) | 08 | per add-on, on demand |

Steps 04 to 16 each add the knowledge of one option. The words "the example", "generated" and "passes" in their "Behaviour to prove in phase 2" sections describe what phase 2 will check, not something to run now.

## The optional pilot

After step 03 the maintainer may ask for **one** generation of the lean preset, on one model, to see whether a project can be rebuilt from snippets at all before fifteen more options are written on the same foundations. **The maintainer has asked for it (decided at the start of step 01).** It runs after step 03 and is recorded in `docs/pilot.md` and `docs/generation-log.md`.

## Exit

Phase 1 ends when all steps are done and `docs/unverified.md` lists everything that awaits proof. Then phase 2 begins.
