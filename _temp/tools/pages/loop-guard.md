# Loop guard, C#

Applies when `addons` contains `loop-guard`. Project `{Name}.Shared` (folders `Application/`, `Domain/`, `Exceptions/`). Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

## How it fits together

```
CommandContext.Chain   the names of the commands that led here, oldest first
EventBase.Chain        stamped by EventContext from the raising command's context, like the tenant and the language
FromEvent              copies the event's chain into the reaction's context
CommandPipeline        chain := chain + this command's name; length > LoopGuardConstants.MaxChain -> LoopDetectedException
                       (500 loop_detected; the message names the chain: "A -> B -> A -> ...")
```

- The chain crosses every transport: it is part of the event, so the outbox payload and the bus envelope carry it, as they carry the user.
- The limit counts commands, not events: a reaction always runs a command, so a loop of handlers is a loop of commands.
- A loop is a bug, so it is a 500 with an error reference, logged at error level with the chain; the command that hit the limit runs nothing, and its transaction (if any) rolls back. With an outbox, the consumer's retries fail the same way and the message ends in the error queue or the outbox's backoff, where the chain is in the log.
- `MaxChain` is 16 by default: deep enough for any real chain of reactions in one business action, short enough to stop a loop within a second.

## The context and the event

`CommandContext` gains:

```csharp
    public IReadOnlyList<string> Chain { get; init; } = [];
```

and `FromEvent` copies it: `Chain = @event.Chain ?? []`. `EventBase` gains `public IReadOnlyList<string> Chain { get; init; }`; `EventContext.AddRaised` adds `Chain = commandContext.Chain` to the `with` that stamps the tenant and the language.

`LogFields.Of(command)` and `LogFields.Of(@event)` add `["Chain"] = string.Join(LoopGuardConstants.Separator, ...Chain ?? [])`, so every log line shows how it was reached.

## The check

File `src/{Name}.Shared/Application/LoopGuardConstants.cs`:

```csharp
public static class LoopGuardConstants
{
    public const int MaxChain = 16;
    public const string Separator = " -> ";
}
```

In `CommandPipeline.ExecuteAsync`, right after `CommandContext.Complete`:

```csharp
        contextCommand.Context = contextCommand.Context with { Chain = [.. contextCommand.Context.Chain, typeof(TCommand).Name] };

        if (contextCommand.Context.Chain.Count > LoopGuardConstants.MaxChain)
        {
            throw new LoopDetectedException(contextCommand.Context.Chain);
        }
```

File `src/{Name}.Shared/Exceptions/LoopDetectedException.cs`:

```csharp
public class LoopDetectedException(IReadOnlyList<string> chain) : ExceptionBase(
    "loop_detected",
    HttpStatusCode.InternalServerError,
    $"Commands triggered each other more than {LoopGuardConstants.MaxChain} times: {string.Join(LoopGuardConstants.Separator, chain)}.",
    null,
    new Dictionary<string, object> { ["chain"] = string.Join(LoopGuardConstants.Separator, chain) });
```

With `exceptions` = `catalogued`, `loop_detected` joins the catalogue (`failures.md`).

## Tests

`CommandPipelineTests` gains: the context's chain ends with the command's name after `ExecuteAsync`; a context whose chain already holds `MaxChain` names throws `LoopDetectedException` before any validator runs, and its message names every command. `EventContextTests` gains: a raised event carries the command's chain. `CommandContextTests` gains: `FromEvent` copies the chain.

The phase 2 proof is a scenario with two fake handlers that trigger each other, in `TestSupport/Shared/Application/PingPong.cs` (a `PingCommand` whose handler raises `Pinged`, a sync handler of `Pinged` that sends `PongCommand`, whose handler raises `Ponged`, whose sync handler sends `PingCommand`): `LoopGuardTests.Send_WithTwoHandlersThatTriggerEachOther_Should_StopAtTheLimitAndNameTheChain` asserts `LoopDetectedException`, a chain of `MaxChain + 1` names alternating `PingCommand` and `PongCommand`, and no more than `MaxChain` handled commands. `scripts/conformance/loop-guard.sh` checks the chain on the example's log lines.

## Files this add-on changes

`CommandContext.cs`, `EventBase.cs`, `EventContext.cs`, `CommandPipeline.cs`, `LogFields.cs`; with RabbitMQ nothing more, since the envelope carries the whole event.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `LoopGuardConstants.cs` | `{Name}.Shared.Application` | none |
| `LoopDetectedException.cs` | `{Name}.Shared.Exceptions` | `{Name}.Shared.Application`, `System.Net` |
| `PingPong.cs`, `LoopGuardTests.cs` | `{Name}.Tests.TestSupport.Shared.Application`, `{Name}.Tests.Shared.Application` | `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `{Name}.Shared.Exceptions`, `Microsoft.Extensions.DependencyInjection` |

## Decision record

`docs/adr/0029-loop-guard.md`, in `project-docs-adr.md`.
