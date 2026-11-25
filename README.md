# mortis-starter

Four Claude Code skills that start a backend project from a short interview and keep it in shape as it grows. Today they generate .NET 10 backends built from domain-driven-design building blocks (commands through a dispatcher, aggregates that raise events, PostgreSQL through EF Core, an outbox, optionally RabbitMQ). Go is supported for the lean preset (pgx, PostgreSQL), and a Go worker can share the outbox with a C# service (`docs/roadmap.md`).

| Skill | What it does | State |
|---|---|---|
| `mortis-architect` | Interviews you and writes `MORTIS.md`, the project's decisions file | Written; its validator and preset writer are tested, the interview itself has not been run end to end |
| `mortis-scaffold` | Generates the project from `MORTIS.md` and the C# reference pages, then runs `verify.sh` | Proved: a mid-size model generated lean and standard projects that pass every check (`examples/csharp/`); advanced is built and verified as a reference solution |
| `mortis-develop` | Adds to a generated project by recipe (aggregate, child entity, command, query, event, handler, consumer, read model, tenant ownership, translated field, key type, turning an option on), test first | Written; exercised in phase 2 step 06 |
| `mortis-review` | `review.sh` runs 14 mechanical checks of the project's rules; a checklist covers the judgement calls | Written; its fixtures pass |

`plans/PROGRESS.md` has the exact state of every step.

## Two-minute start

Requirements: Claude Code, the .NET SDK 10, Docker (for PostgreSQL, and RabbitMQ in the advanced preset).

```bash
# as a plugin
claude plugin marketplace add <this repository's URL or path>
claude plugin install mortis-starter@mortis

# or by hand
bash scripts/install.sh            # copies skills/* into ~/.claude/skills
bash scripts/install.sh --link     # symlinks instead, so edits here take effect at once
```

Then, in an empty folder, ask Claude Code: "start a backend for a B2B invoicing product, C# and PostgreSQL". `mortis-architect` asks a few questions and writes `MORTIS.md`; `mortis-scaffold` writes the solution and runs `verify.sh` until it passes. Start the database with the generated `docker-compose.yml`, apply the migration (`dotnet ef database update --project src/<Name>.Infrastructure`) and run `src/<Name>.Api`.

Afterwards: "add an Invoice aggregate with lines" (`mortis-develop`), "review this branch" (`mortis-review`).

## Presets

| Preset | For | What it leaves out |
|---|---|---|
| `lean` | a prototype, one developer, a small service nobody else consumes | the outbox, a worker, authentication, translated messages, integration tests |
| `standard` | a real product with one team and an API for a front end | a broker, tenants, translated messages, read models, a separate worker, architecture tests |
| `advanced` | one service of a platform: a broker, tenants, a separate worker, many developers | nothing on the list; the most to run and to learn |

Every value of a preset can be changed in the interview. What each option costs, and which options require others, is in `skills/mortis-architect/references/options/` and `presets.md`. Two add-ons are written but not proved and are in no preset: `storage` and `contexts` (decision 53).

## Adding an option later

Edit the row in `MORTIS.md`, then ask `mortis-develop` to apply it ("turn on the outbox"). Its recipe reads the option's page, changes the files the page lists, adds the decision record, and runs `verify.sh`.

## What a generated solution looks like

```
src/<Name>.Shared          Entity, AggregateRoot, EventBase, EventContext, sync handlers, event consumers, commands, the pipeline, exceptions
src/<Name>.Domain          Aggregates/<Plural>/: aggregate, children, value objects, Events/, repository interface
src/<Name>.Application     commands (record + validator + handler), queries, DTOs, handlers that send commands
src/<Name>.Infrastructure  EF Core + PostgreSQL: repositories, mappings, unit of work, outbox, messaging, migrations
src/<Name>.Api             HTTP host
src/<Name>.Worker          the outbox and the event consumers (when the preset has a separate worker)
src/<Name>.Console         runs any registered command by name
test/<Name>.Tests          fakes, builders, tests; test/<Name>.IntegrationTests from the integration level
```

`skills/mortis-architect/references/architecture/` explains, without a language, where each piece belongs and how a request and its events travel. `skills/mortis-scaffold/references/csharp/` is how each piece is written in C#.

## How the skills are kept honest

- Three reference solutions in `_temp/variants/` (lean, standard, advanced) are the source of truth (decision 37). Every snippet on a page is checked against them, and every one passes `verify.sh`, which builds, tests, formats and runs the conformance scripts against a real PostgreSQL (and RabbitMQ for advanced).
- Generated projects are never fixed by hand: a defect a generation run finds is fixed in the skills and logged in `docs/generation-log.md`.
- [docs/decisions.md](docs/decisions.md): every decision about these skills. [docs/unverified.md](docs/unverified.md): what is written but not yet proved. [CHANGELOG.md](CHANGELOG.md): what changed.

## Checking the skills themselves

```bash
bash tests/check_installable.sh                 # no project source in skills/, snippet rules, no origins
python3 -I tests/check_docs.py                  # pages, manifest, kernel shape and schema agree with the variants
python3 -I tests/check_architecture.py          # the language-neutral layer
python3 -I tests/test_validate_decisions.py     # the MORTIS.md validator
python3 -I tests/check_variants.py              # the three reference solutions agree with each other and the manifest
python3 -I tests/check_review.py                # review.py: no finding on the variants, exactly the planted one on each fixture
bash tests/check_interop.sh                     # the RabbitMQ bus against MassTransit 8 (needs RabbitMQ)
for p in lean standard advanced; do bash skills/mortis-scaffold/scripts/verify.sh _temp/variants/$p; done
for p in lean standard; do bash skills/mortis-scaffold/scripts/verify.sh examples/csharp/$p; done
```

`verify.sh` needs `PGHOST`, `PGUSER`, `PGPASSWORD` for a PostgreSQL 16, and `psql`, `curl`, `python3`. Continuous integration (`.github/workflows/checks.yml`) runs the document checks and `verify.sh` on the saved examples; generation runs are made by hand before a release.

## Licence

MIT, see [LICENSE](LICENSE).
