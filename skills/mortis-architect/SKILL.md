---
name: mortis-architect
description: Interviews the person starting a backend project and writes MORTIS.md, the decisions file every later step reads. Picks a lean, standard or advanced preset from the description, asks only about what is doubtful (outbox and event delivery, broker, multi-tenancy, translated messages, exceptions, queries, testing, authentication, naming), refuses combinations that cannot work, and records a decision for every change from the preset. Also explains where code belongs (layers, aggregates, commands, events). Use whenever someone wants to start, plan, bootstrap or architect a new backend, API or service, asks which outbox, broker or tenancy model to choose, asks "where does this code go", or says "lean", "standard" or "advanced" project, even when they never mention DDD.
---

# Architect a project

This skill turns "what are you building" into `MORTIS.md`: one markdown file with one table of decisions. Nothing is generated here; `mortis-scaffold` reads the file and builds the project, `mortis-develop` and `mortis-review` read it to know the project's rules.

## The path

1. Read the description. Pick the preset it fits and take every value it states.
2. Ask only what the description leaves doubtful (`references/interview.md`). At most four questions per round, the recommended value first, one line on the cost of each option.
3. Show the table, the rules that fired and what was chosen by default. Ask once for confirmation.
4. Write the file and the decision records, then validate it.
5. Say the next step: the `mortis-scaffold` skill.

When there is nobody to ask, pick the preset that fits, say which one at the top of the answer, and write the file. Never stall on a question.

## Commands

Run from this skill's folder (or give the full path). `-I` keeps the run isolated.

```
python3 -I scripts/new_decisions.py --name Acme --language csharp --database postgres --preset standard \
    --set events.outbox=background --why events.outbox="events must not be lost" --out MORTIS.md
python3 -I scripts/validate_decisions.py MORTIS.md
python3 -I scripts/decisions.py MORTIS.md get events.outbox
```

- `new_decisions.py` writes a preset as a valid file; `--set key=value` changes a row, `--why key=reason` fills its reason. It refuses a result that breaks a rule.
- `validate_decisions.py` prints every problem with its line, rule and fix, then `MORTIS.md is valid` (exit 0) or the count (exit 1). Notes (R7, R8) are not problems.
- `decisions.py FILE get KEY` prints a resolved value, with the preset's value for a row that is absent. Scripts and `verify.sh` use it.

## What `MORTIS.md` looks like

One table under `## Decisions` is the only part scripts read; everything else is for people and assistants and may be edited freely. A key left out takes the preset's value. The keys, values and rules are in `references/schema.json`; the table by preset is in `references/presets.md`.

## Reference map

Read only what the situation needs.

| File | Read it when |
|---|---|
| `references/interview.md` | you are about to ask questions: the order, which description triggers which question, the wording, the unattended path, how to write the file |
| `references/presets.md` | choosing a preset, or explaining what lean, standard and advanced contain and who each is for |
| `references/schema.json` | you need the exact keys, values, defaults and rules R1 to R13 (the validator reads it; do not copy values from memory) |
| `references/options/project.md` | `version`, `name` or `preset` |
| `references/options/language.md` | choosing the language (only those marked available) |
| `references/options/database.md` | postgres or mongo, and what mongo changes |
| `references/options/events.md` | `events.outbox` and `events.transport`: the delivery models and what each can lose |
| `references/options/keys.md` | guid, int or long keys |
| `references/options/naming.md` | the four project naming keys; read only when an existing database, API or consumer must be matched |
| `references/options/exceptions.md` | minimal, coded or catalogued failures |
| `references/options/queries.md` | commands, split or read models |
| `references/options/tenancy.md` | `tenancy.mode`, `tenancy.key`, `tenancy.resolver` |
| `references/options/languages.md` | translated messages or data: `languages.*` |
| `references/options/testing.md` | the four testing rungs |
| `references/options/auth.md` | none or jwt |
| `references/options/hosts.md` | `hosts.worker`: none, in-api, separate |
| `references/options/addons.md` | any of the twelve add-ons |
| `references/options/example.md` | whether to generate the worked example |
| `references/adr-template.md` | writing the decision record for a value that differs from the preset |
| `references/architecture/` | explaining or deciding where code belongs; start with `layers.md`, then `placement-guide.md`; `building-blocks.md` for one block; `events.md` for sync, async and outbox; `tenancy.md` for the signed-in caller and tenant-owned data; `languages.md` for translated messages and data; `scenarios.md` for worked examples; `request-lifecycle.md` for the commit order; `pseudo-code.md` for the notation; `glossary.md` for a term |

## Rules for this skill

- Every option you describe says what it **costs**. "More robust" is not information.
- Offer only values the schema lists, and only languages marked available. Go is listed but not available yet; say so and offer C#.
- Never offer a combination the rules refuse; when a choice triggers a rule, say which values change with it and change them.
- Identifier casing is the language's and is never asked. The four `naming.*` keys are asked only when the description mentions an existing database, API or consumer.
- The `Why` column holds the user's own reason, in their words.
- Do not generate code or project files here, other than `MORTIS.md` and `docs/adr/` records.
- When the user asks where something belongs, answer from `references/architecture/` and name the layer and the block.
