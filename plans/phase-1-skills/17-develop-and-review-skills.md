# Phase 1, step 17 — The `mortis-develop` and `mortis-review` skills

Needs: phase 1 steps 03 to 16. Size: large. No C# is written, built or generated in this step.

## Goal

After the project exists, two skills keep it in shape: one adds things the way the chosen options require, one checks a change against the rules. Both read `MORTIS.md`, so their advice differs between a lean project and an advanced one.

## Design

`mortis-develop`:
```
SKILL.md                      which recipe to follow for which request
references/recipes/           add-aggregate, add-child-entity, add-value-object, add-command, add-query,
                              add-event, add-sync-handler, add-async-handler, add-read-model, add-consumer,
                              make-aggregate-tenant-owned, add-translated-field, change-a-key-type
references/<lang>/            the language rendering of each recipe; points at the mortis-scaffold reference page of each option
references/canonical-shapes.md   the closed vocabulary of class names and the reference slice to copy from
references/tdd-order.md       the order in which a feature is written: aggregate test, aggregate, service test, …
scripts/new_aggregate.sh      creates the files of an aggregate from the example's shape (optional helper)
```
- A recipe is a list of files to touch, in order, each with the rule that applies and the test that proves it; conditional lines are tagged with the decision they depend on (`[tenancy]`, `[outbox]`, `[queries=split]`).
- The first thing the skill does is read `MORTIS.md`; without one it says so and falls back to the lean recipes.
- It also covers turning an option on later: "add the outbox to this project" changes `MORTIS.md`, writes the option's files from the same references `mortis-scaffold` uses, adapts the existing files the option touches, and runs `verify.sh`.

`mortis-review`:
```
SKILL.md
scripts/review.sh             mechanical checks on a diff or a folder; exit code and a report
references/checklist.md       the judgement checks a script cannot do, per layer
references/findings-format.md
```
- Mechanical checks, each mapped to a rule in the project's `AGENTS.md`: comments in code, private methods in orchestration classes, a handler holding a business rule (heuristics: branching on aggregate state), a host naming a handler type, a missing registration, a writing command without a context, an `IMultiTenant` table without `tenant_id` in a unique index, an event handler that calls a service, a test class with a helper method, a banned API, a queue event whose contract changed without the golden file.
- Judgement checks follow the placement guide of the architecture layer.
- The report lists findings with file, line, the rule and a concrete fix, most severe first, and says which checks were not run and why.

Also added to what a generated project carries and what these skills maintain: an incident log (bugs already hit and the test that guards each), and a "where else must this change" checklist per option.

## Deliverables

The two skills; eval prompts written (at least five each, including a lean and an advanced project) but not run; fixtures of deliberately wrong changes for the review script, written as small text files with the expected findings; `scripts/install.sh` updated; every page and check listed in `docs/unverified.md`.

## Acceptance (this phase)

- [ ] Every recipe names, for each file it touches, the reference page and the rule that applies, and tags conditional lines with the decision they depend on; `tests/check_docs.py` checks the tags against the schema.
- [ ] `review.sh` passes a syntax check and lists its checks with `--dry-run`.

## Behaviour to prove in phase 2


- [ ] On each fixture, `review.sh` reports the planted violation and nothing else.
- [ ] With the skill, a fresh model adds an aggregate with a child entity, a command and an async handler to a project generated with the standard preset; the result builds with 0 warnings, its tests pass and `review.sh` is clean. Without the skill, the same prompt fails at least one of those (record both runs).
- [ ] The same "add an aggregate" prompt on a tenancy-on project produces a tenant-owned aggregate with the index and tests; on a lean project it does not mention tenants.

## Out of scope

Automatic refactoring of an existing non-Mortis codebase (the "adopting in an existing project" reference stays a manual guide).

## Traps

- Decision 28: `mortis-review` judges a command handler that changes several aggregates by the second-aggregate test in the placement guide (is the second change the intent of the action, or a consequence?), never by counting calls. `mortis-develop` adds a consequence as an event handler, never as a new line in an existing command handler.
