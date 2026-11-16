# Changelog

All notable changes to the skills and the template. Newest first.

## 2026-11

### Changed
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
