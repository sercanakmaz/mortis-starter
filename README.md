# mortis-starter

A project starter for backends, delivered as Claude Code skills. Today it starts and grows a .NET backend built from domain-driven-design building blocks; it is becoming a starter where you choose the methods and technologies your project needs (see `docs/roadmap.md`).

## Status

| Skill | State |
|---|---|
| `mortis-architect` | **Written, not yet proven.** Interviews you and writes `MORTIS.md`, the decisions file |
| `mortis-scaffold` | **Being rebuilt (phase 1).** Generates a project from `MORTIS.md` and the C# reference pages, then runs `verify.sh`. Not proven until phase 2 |
| `mortis-develop` | **Written, not yet proven.** Adds to a generated project by recipe (aggregate, child entity, command, query, event, handlers, consumer, read model, tenant ownership, translated field, key type, turning an option on), test first |
| `mortis-review` | **Written, not yet proven.** `review.sh` runs 14 mechanical checks of the project's rules; a checklist covers the judgement calls; findings come with file, line, rule and fix |

## Install

```bash
bash scripts/install.sh            # copies skills/* into ~/.claude/skills
bash scripts/install.sh --link     # symlinks instead, so edits here take effect at once
```

To use a skill in one project only, copy its folder into that project's `.claude/skills/`.

## Use

In Claude Code, describe what you are building: "start a backend for a B2B invoicing product, C# and PostgreSQL". `mortis-architect` writes `MORTIS.md`; `mortis-scaffold` generates the project from it and runs `verify.sh`. Afterwards, `mortis-develop` adds to it ("add an Invoice aggregate with lines") and `mortis-review` checks a change ("review this branch").

The old copy script still works from the repository while phase 1 runs: `bash _temp/template/scripts/scaffold.sh Acme --out ~/code`.

Requirements for a scaffolded solution: .NET SDK 10, and PostgreSQL (the generated `docker-compose.yml` starts one).

## What a scaffolded solution looks like

```
src/<Name>.Shared          Entity, AggregateRoot, EventBase, EventContext, BaseSyncEventHandler, BaseEventConsumer, ICommand / IUserCommand, ICommandDispatcher, command pipeline, exceptions
src/<Name>.Domain          Aggregates/<Plural>/ : aggregate, children, value objects, Events/, repository interface; Services/ when needed
src/<Name>.Application     commands (record + validator + handler), DTOs, event handlers that send commands
src/<Name>.Infrastructure  EF Core + PostgreSQL: repositories, mappings, unit of work, event consumer dispatcher, migrations
src/<Name>.Api             HTTP host
src/<Name>.Console         runs any registered command by name, plus scenarios: dotnet run --project src/<Name>.Console -- <Command> '<json>'
test/<Name>.Tests          fakes, builders, tests
```

`skills/mortis-architect/references/architecture/` explains, without a language, where each piece belongs and how a request and its events travel. `skills/mortis-scaffold/references/csharp/` is how each piece is written in C#.

## Why it is built this way

- [docs/decisions.md](docs/decisions.md): decisions about these skills, and what is still open.
- [docs/options-catalog.md](docs/options-catalog.md) and [docs/roadmap.md](docs/roadmap.md): where this is going, a project starter with choosable methods and technologies.
- [plans/](plans/README.md): the brief for each step of that work.
- [CHANGELOG.md](CHANGELOG.md): what changed.
- `_temp/variants/<preset>/docs/adr/` explains the backend's own design, as each preset's project gets it.

## Checking the skills themselves

```bash
bash tests/check_installable.sh                 # no project source in skills/, snippet rules, no origins
python3 -I tests/check_docs.py                  # pages, manifest, kernel shape and schema agree
python3 -I tests/check_architecture.py          # the language-neutral layer
python3 -I tests/test_validate_decisions.py     # the MORTIS.md validator
python3 -I tests/check_variants.py              # the three reference solutions agree with each other and the manifest
python3 -I tests/check_review.py                # review.py: no finding on the variants, exactly the planted one on each fixture
bash tests/check_interop.sh                     # the RabbitMQ bus against MassTransit 8 (needs RabbitMQ)
for p in lean standard advanced; do bash skills/mortis-scaffold/scripts/verify.sh _temp/variants/$p; done   # each passes verify.sh
PGHOST=localhost PGUSER=postgres PGPASSWORD=postgres bash tests/smoke.sh
```

`smoke.sh` belongs to the old copied template and is deleted with it. It checks that the reference docs match, then scaffolds three solutions and checks them against a real PostgreSQL: build, unit tests, the banned-API guard, migrations, a committed flow, sync and event consumers on one event, a rolled-back flow, concurrent stock updates, the Api's status codes, and kernel-only mode. It needs the .NET SDK 10, `psql`, `curl` and `python3`.
