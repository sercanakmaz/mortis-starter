# DDD backend skills for .NET

Claude Code skills that start and grow a .NET backend built from domain-driven-design building blocks. The shape comes from [`csharp_boilerplate`](https://github.com/sercanakmaz/csharp_boilerplate); the rules and safeguards come from what the Zeyrek backend learned on top of it.

## Status

| Skill | State |
|---|---|
| `ddd-backend-scaffold` | **Ready.** Creates a solution (with a worked Order / Customer / Product example, which you can leave out), or copies only the building blocks into an existing solution. Includes sync and async event handlers |
| `ddd-backend-develop` | Planned (phase 2): adding aggregates, child entities, value objects, events, commands, mappings and tests |
| `ddd-backend-review` | Planned (phase 3): a script and checklist that check a change against the rules |

## Install

```bash
bash scripts/install.sh            # copies skills/* into ~/.claude/skills
bash scripts/install.sh --link     # symlinks instead, so edits here take effect at once
```

To use a skill in one project only, copy its folder into that project's `.claude/skills/`.

## Use

In Claude Code, ask for what you want: "scaffold a new .NET backend called Acme.Billing". Or run the script yourself:

```bash
bash skills/ddd-backend-scaffold/scripts/scaffold.sh Acme --out ~/code
cd ~/code/Acme && dotnet build && dotnet test
```

Add `--no-example` for an empty solution without the Order / Customer / Product example.

Requirements for a scaffolded solution: .NET SDK 10, and PostgreSQL (the generated `docker-compose.yml` starts one).

## What a scaffolded solution looks like

```
src/<Name>.Shared          Entity, AggregateRoot, EventBase, EventContext, BaseSyncEventHandler, BaseAsyncEventHandler, ICommand / IUserCommand, ICommandDispatcher, command pipeline, exceptions
src/<Name>.Domain          Aggregates/<Plural>/ : aggregate, children, value objects, Events/, service, repository interface, module
src/<Name>.Application     commands (record + validator + handler), DTOs, event handlers that send commands
src/<Name>.Infrastructure  EF Core + PostgreSQL: repositories, mappings, unit of work, async handler dispatcher, migrations
src/<Name>.Api             HTTP host
src/<Name>.Console         runs any registered command by name, plus scenarios: dotnet run --project src/<Name>.Console -- <Command> '<json>'
test/<Name>.Tests          fakes, builders, tests
```

`skills/ddd-backend-scaffold/assets/csharp/docs/architecture.md` explains the request flow, the event pipeline and the persistence rules. `assets/csharp/AGENTS.md` lists the rules and what enforces each one. `skills/ddd-backend-scaffold/references/event-handlers.md` explains when to use a sync handler (inside the transaction, can veto) and when an async one (after the commit, eventual consistency).

## Why it is built this way

- [docs/decisions.md](docs/decisions.md): decisions about these skills, and what is still open.
- [CHANGELOG.md](CHANGELOG.md): what changed.
- The template's `docs/adr/` explains the backend's own design: the command dispatcher, commands as interfaces, the user on the command, event handlers and the Console.

## Checking the skills themselves

```bash
PGHOST=localhost PGUSER=postgres PGPASSWORD=postgres bash tests/smoke.sh
```

This checks that the skill's reference docs match the template and the example, then scaffolds three solutions and checks them against a real PostgreSQL: build, unit tests, the banned-API guard, migrations, a committed flow, sync and async handlers on one event, a rolled-back flow, concurrent stock updates, the Api's status codes, and kernel-only mode. It needs the .NET SDK 10, `psql`, `curl` and `python3`.
