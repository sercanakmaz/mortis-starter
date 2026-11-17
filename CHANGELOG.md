# Changelog

All notable changes to the skills and the template. Newest first.

## 2026-11

### Added
- Phase 1 step 01: the language-neutral architecture layer in `skills/mortis-architect/references/architecture/` (layers, 18 building blocks, request lifecycle, events, 50-row placement guide, 10 scenarios, notation, glossary), `tests/check_architecture.py` and placement fixtures. Ledger `docs/unverified.md` and `docs/generation-log.md` created. The pilot generation after step 03 is switched on.

- Phase 1 step 02: the `mortis-architect` skill: interview, `MORTIS.md` schema (25 keys, rules R1 to R12), shared parser, validator, preset writer, 15 option pages, four evals; 26 validator tests.

- Architecture layer: explicit cardinality (one command can raise many events, one event can have many handlers), scenario 11, and a decision cheat sheet with a flow diagram and common pitfalls in the placement guide.

- Architecture layer, decision 28: command handlers are use cases that may change several aggregates when the action is about all of them; domain services hold logic that fits no single aggregate; new anti-corruption layer block; the cheat sheet decides between the command handler and an event handler for a second aggregate.

- Decisions 28 to 30: use cases may change several aggregates; standard preset has no translated messages; one repository is one service with one `MORTIS.md`.

- Phase 1 step 03: `mortis-scaffold` rebuilt as knowledge and checks: 20 C# pages with member snippets, file manifest, kernel shape, naming table, `verify.sh` with structure, house-rule, shape and conformance checks, `tests/check_installable.sh`, `tests/check_docs.py` rewritten. The example domain is specified in roles (`example-domain.md`). Old per-layer references removed.

- Pilot generation of the lean preset (mid-size model): 11 of 12 checks, the failure a skill defect (fixed). Procedure reordered, usings tables on every page, worked-slice order fixed (`docs/pilot.md`).

- Phase 1 step 04: one `Context` on the command (`CommandContext`, `IContextCommand`); events stamped with correlation, causation, tenant and language by `EventContext`; ADR 0007; a house rule and three conformance checks.

- Decision 31: domain services are pure by default, may read through domain interfaces (narrow read ports preferred), never write; `verify.sh` enforces the no-write rule in the Domain project.

### Planned
- The repository becomes a project starter with choosable options. Added `docs/options-catalog.md`, `docs/roadmap.md`, three design specs under `docs/specs/` and a brief for each of 21 steps under `plans/`.
- The skills will hold knowledge and checks only. The C# solution will move out of the skill to `examples/` as a reference used to verify the skills (step 03).

### Changed
- The product is named mortis-starter. The skill `ddd-backend-scaffold` is now `mortis-scaffold`.
- Notes on where ideas came from moved to a temporary, git-ignored `_sources/` folder.
- The C# solution and `scaffold.sh` moved out of the `mortis-scaffold` skill to the temporary `_temp/template/`. The skill now holds no project source.
- Commands no longer extend `BaseCommand` / `UserCommand`. They are plain records that implement `ICommand<TContent>`, `IUserCommand` and `ITransactionalCommand`; `ToAuditUser()` is an extension method (ADR 0002).
- Hosts send commands through `ICommandDispatcher.Send(command)` and never name a handler. Handlers are registered with `AddCommandHandler<TCommand, TContent, THandler>()`. Controllers, Console scenarios and event handlers were converted (ADR 0001).
- `CommandScenario<TCommand, TContent>` replaces `CommandScenario<TCommand, TContent, THandler>`.
- The user is data on the command. `IUserContext` is gone; `UserCommandFilter` (Api) and `CommandScenario` (Console) fill it; events carry the user and `AggregateRoot` stamps it (ADR 0003).
- The Console no longer migrates. It runs any registered command by name, plus scenarios (ADR 0005).
- "Use case" was renamed "command".
- One folder per language, `assets/csharp/`, with the example included; the former `templates/` and `examples/` folders are merged. The `--sample` flag became `--no-example`.
- Event handlers moved to the Application layer and send commands (ADR 0004).

### Added
- Sync and async domain event handlers (`BaseSyncEventHandler`, `BaseAsyncEventHandler`, `AsyncEventHandlerDispatcher`) with retry on `DbUpdateConcurrencyException`, and the example flows that use them.
- The Console command runner, a VS Code launch configuration that asks for the name and JSON, and an exit-code contract.
- `ICommandDispatcher`, `CommandDispatcher`, `CommandHandlerExtensions`, `CommandDispatcherBuilder` and `CommandDispatcherTests`.
- Skill references: `architecture-overview`, `domain-layer`, `application-layer`, `event-handlers`, `persistence`, `hosts`, `testing`, `template-internals`, `adopting-in-an-existing-project`.
- Architecture decision records 0001 to 0005 in the template, and `docs/decisions.md` here.
- `tests/check_docs.py`, which compares the references with the template and the example.
- `--no-example` and `--kernel-only` scaffold modes; the `docs/` folder.

### Removed
- `IUserContext`, `BaseCommand`, `UserCommand`, the `sources` and `plan` folders, the separate example folder and the `--sample` flag.

### Checks
- `tests/smoke.sh`: 67 checks. Scaffolded tests: 44 with `--no-example`, 141 with the example. Build: 0 warnings.
