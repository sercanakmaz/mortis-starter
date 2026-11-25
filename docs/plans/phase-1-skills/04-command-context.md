# Phase 1, step 04 — One context on the command (knowledge only)

Needs: phase 1 steps 03. Size: medium. Changes the kernel pages and replaces part of ADR 0002 and 0003. Approved by the maintainer on 2026-11-16. No C# is written, built or generated in this step.

## Goal

Correlation, tenant and language are about to join the user as things a host fills in on a command and an event carries. Adding two `[JsonIgnore]` properties per concern to every command does not scale (ADR 0002 already named this as the next step). Replace `UserId` / `UserEmail` on the command with **one** property that holds everything the host knows about the call.

## Read first

ADR 0002 and 0003. In the existing C# solution `_temp/template/assets/csharp/` (temporary source material; never installed): `IUserCommand.cs`, `UserCommandFilter.cs`, `ConsoleUserExtensions.cs`, `AggregateRoot.cs`, `EventBase.cs`, `AuditUser.cs`, `SystemUser.cs`, and the example's event handlers. In the skill: the pages of `references/csharp/` that describe them, which this step rewrites. The existing solution is **not** changed in this phase; it still shows the old shape until phase 2 regenerates it.

## Design

```
record CommandContext
  user: AuditUser                 (id, email)
  correlationId: Guid
  causationId: Guid or none       the event whose handler sent this command
  tenantId: TenantId or none      used from step 11
  language: text or none          used from step 12

interface IContextCommand          replaces IUserCommand
  context: CommandContext          never read from the request body

record EventContextData            what an event carries of the above
  userId, userEmail, correlationId, causationId, tenantId, language
```

- A writing command is `record X : ICommand<TDto>, IContextCommand, ITransactionalCommand` with one line: `[JsonIgnore] public CommandContext Context { get; set; }`.
- Hosts fill it: the Api filter from claims and headers, the Console from its constants, an event handler with `CommandContext.From(@event)` (which sets the causation id to the event's id, keeps correlation, tenant and language, and uses the event's user for a sync handler and the system user for an async one).
- The handler passes `command.Context.User` where it passed `command.ToAuditUser()`. Service and aggregate signatures do not change.
- `EventBase` carries the event context. `AggregateRoot` stamps the user as today; the unit of work stamps correlation, causation, tenant and language on every raised event from the context of the command being executed, just before the sync handlers run. The command pipeline hands the context to the unit of work at the start of the command; this is the only place it is passed implicitly, and it is scoped to the pipeline.
- `EventBase.CorrelationId` stops reading `Trace.CorrelationManager.ActivityId`.
- A command without `IContextCommand` (a query) has no context and is not stamped.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md` (`0006-one-context-on-the-command.md`).
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

- [ ] A request body containing `"context": {...}` or `"userId"` does not change the context (test).
- [ ] An event raised by a command carries the command's correlation id; a command sent from that event's handler carries the same correlation id and the event's id as causation id; this holds across a sync and an event consumer (tests, and one conformance check that reads both from the log of `stock-flow`).
- [ ] No writing command declares more than one context line (`grep` in `verify.sh`).
- [ ] 0 warnings; all earlier conformance checks pass.

## Out of scope

Reading a correlation header (step 06), tenants (step 11), language (step 12). The fields exist; nothing fills them yet except correlation, which is generated per command when absent.

## Traps

- Do not make the context an ambient service that services read. It travels as data; only the unit of work receives it implicitly, to stamp events.
- A nested command (a sync handler sending a command) must not overwrite the outer command's context in the unit of work; keep a stack or stamp events at raise time per command.

## Notes (done)

- Kernel: `CommandContext` (user, correlation, causation, tenant, language; `Complete`, `FromEvent`), `IContextCommand` (replaces `IUserCommand` and `ToAuditUser`), `CommandPipeline` takes `EventContext` and runs a context command between `EnterCommand` and `LeaveCommand`; `EventContext` keeps a stack of command contexts and stamps every raised event with the top one (nested commands are handled by the stack, as the trap asked); `EventBase` gains `CausationId`, `TenantId`, `Language` and no longer reads `Trace.CorrelationManager`; the handler bases give `ContextOf(@event)` (event user for sync, system user for async); `ContextCommandFilter` replaces `UserCommandFilter`; the correlation middleware hands its id to the filter through `HttpContext.Items`; the Console uses one correlation id per run; `LoggingEventDispatcher` logs correlation and causation.
- `TenantId` is a `string` for now; step 11 decides whether `tenancy.key` makes it typed.
- The changed and new kernel files are written in full under `_temp/tools/overlay/` (temporary, deleted with `_temp/`); the page generator, the manifest builder, `kernel-shape.txt` and `tests/check_docs.py` read the overlay before the template. Example code is converted by fixed text replacements in the generator.
- The decision record is `0007-one-context-on-the-command.md` (0006 was taken by decision 28). ADRs 0002 and 0003 say they were superseded by it.
- `verify.sh` house rules now fail a command that declares more than one `[JsonIgnore]` line or a user, correlation or tenant property of its own; shown failing on the pilot project's old commands.
- Conformance: three new checks in `example-console.sh`, read from the `stock-flow` log: one correlation id for the run, the order's `Cancelled` caused by a `LineCancelled` (sync), `StockIncreased` caused by a `LineCancelled` (async).
- Found, not done: the conformance relies on the log line format "Published <wire> <id> correlation <id> causation <id>"; step 16 should state it as a contract for every language.
