# Phase 1, step 02 — The decision model and the `mortis-architect` skill

Needs: phase 1 step 01. Size: medium. No project is generated; the interview evals only produce a `MORTIS.md`.

## Goal

A skill that interviews the person starting a project and writes `MORTIS.md`, plus a validator for that file. No code is generated in this step.

## Read first

`docs/specs/decisions-file.md` (the starting design), `docs/options-catalog.md`, the architecture layer from step 01. For how a skill is laid out and tested, the skill-creator guidance: a short `SKILL.md`, details in `references/`, a description that says when to trigger.

## Design

```
skills/mortis-architect/
  SKILL.md                      under 300 lines: when to use, the interview, the output, the hand-off to mortis-scaffold
  references/architecture/      from step 01
  references/options/<key>.md   one per key of MORTIS.md: what it is, each value, when to choose it, what it costs, which rules it triggers, a two-line example
  references/presets.md         the three presets as complete decision tables, and who each is for
  references/interview.md       the question order, the wording of each question, which answers skip which questions
  scripts/validate_decisions.py reads MORTIS.md; exit 0 or a list of broken rules with the rule number and a fix
  scripts/new_decisions.py      writes a preset's MORTIS.md with a given name, language and database
  references/adr-template.md
```

The interview follows `docs/specs/decisions-file.md` "Interview order". Questions are multiple choice, at most four per round, the recommended value first. Each option's description is one line on its cost, taken from `references/options/`. When there is no way to ask (an unattended run), the skill picks the preset that fits the request, says which one at the top of its answer, and writes the file.

Output of a run: `MORTIS.md`; `docs/adr/NNNN-<slug>.md` for every value that differs from the preset (context, the options with their cost, the choice); a summary table; the next command (`mortis-scaffold`).

`references/options/naming.md` covers the four project naming choices (`naming.database`, `naming.json`, `naming.routes`, `naming.events`), says plainly that identifier casing is the language's and is never asked, and marks `naming.events` as a choice that changes wire names. The interview asks a naming question only when the description mentions an existing database, API or consumer.

The validator implements rules R1 to R11. It accepts for `language` whatever languages have a manifest, so a new language needs no validator change. `MORTIS.md` is markdown with one fixed table that scripts read (the parsing rules are in the spec); `scripts/decisions.py` is the single parser the validator and `verify.sh` share. The list of values and rules lives in one data file (`references/schema.json`) that the validator, the presets and the documentation checker all read, so they cannot drift.

## Deliverables

- The skill folder above; `references/options/` covers every key in the schema.
- `tests/test_validate_decisions.py` (standard library `unittest`): one passing and one failing file per rule, and the three presets.
- `tests/check_docs.py` extended: every key and value in `schema.json` has a section in `references/options/`, and the tables in `docs/specs/decisions-file.md` match `schema.json`.
- `evals/` in the skill with three prompts (a weekend blog API; a B2B product with two developers; a platform with several services and tenants) and, for each, the values that must come out.
- Update `docs/specs/decisions-file.md` where the design changed, and `scripts/install.sh` to install both skills.

## Acceptance

- [ ] `python3 -I -m unittest tests/test_validate_decisions.py` passes; each rule has a test that fails when the rule is removed.
- [ ] The three eval prompts, run by a fresh model with the skill, produce files that pass the validator and contain the expected values.
- [ ] "lean, C#, PostgreSQL, called Acme" produces a valid file with no question asked.
- [ ] `SKILL.md` names every reference file and says when to read it.
- [ ] The parser reads a `MORTIS.md` whose prose sections were edited by hand, and rejects one with a duplicate key, an unknown key or an unknown value, naming the row.

## Out of scope

Generating anything from the decisions (phase 2). Go values are listed but marked "not available yet"; rule R11 makes the validator refuse them until phase 3.

## Traps

- The interview must not become a forty-question form. The preset answers almost everything.
- Every option text must say what the choice *costs*; "more robust" is not information.
