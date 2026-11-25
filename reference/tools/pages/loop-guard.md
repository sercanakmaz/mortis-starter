# Loop guard, C#

Applies when `addons` contains `loop-guard`. Project `{Name}.Shared` (folders `Application/`, `Domain/`, `Exceptions/`). Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Built** in the advanced reference solution (decision 52): compiled, tested and verified there.

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
    public const string LoopDetectedCode = "loop_detected";
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
    LoopGuardConstants.LoopDetectedCode,
    HttpStatusCode.InternalServerError,
    $"Commands triggered each other more than {LoopGuardConstants.MaxChain} times: {string.Join(LoopGuardConstants.Separator, chain)}.",
    null,
    new Dictionary<string, object> { ["chain"] = string.Join(LoopGuardConstants.Separator, chain) });
```

With `exceptions` = `catalogued`, `LoopGuardConstants.LoopDetectedCode` joins `ErrorCatalogue.Codes` (using `{Name}.Shared.Application`), and with translated messages every `messages.<language>.json` gets a `loop_detected` text with a `{chain}` placeholder (`MessageLocalizerTests` fails otherwise), for example `"loop_detected": "Commands triggered each other too many times: {chain}."`.

## Tests

`CommandPipelineTests` gains:

@@member variant:advanced/test/Boilerplate.Tests/Shared/Application/CommandPipelineTests.cs::CommandPipelineTests::ExecuteAsync_WithAContextCommand_Should_EndTheChainWithTheCommandsName@@

@@member variant:advanced/test/Boilerplate.Tests/Shared/Application/CommandPipelineTests.cs::CommandPipelineTests::ExecuteAsync_WithAFullChain_Should_ThrowLoopDetectedBeforeAnyValidatorRuns@@

(using `FluentValidation` for `InlineValidator`). `EventContextTests` gains:

@@member variant:advanced/test/Boilerplate.Tests/Shared/Domain/EventContextTests.cs::EventContextTests::AddRaised_InsideACommand_Should_CarryTheCommandsChain@@

`CommandContextTests` gains:

@@member variant:advanced/test/Boilerplate.Tests/Shared/Application/CommandContextTests.cs::CommandContextTests::FromEvent_Should_CopyTheEventsChain@@

`TestSupport/Shared/Application/PingPongLog.cs` holds two context commands that trigger each other: `PingCommand` and `PongCommand` (each `ICommand<int>, IContextCommand` with a `[JsonIgnore]` context), the events `Pinged` and `Ponged` (`: EventBase`), a `PingPongLog` (a `List<string> Handled`), and four handlers:

@@members variant:advanced/test/Boilerplate.Tests/TestSupport/Shared/Application/PingPongLog.cs::PingCommandHandler@@

`PongCommandHandler` is the same with `Pong`/`Ponged`; the sync handlers send the other command with the event's context:

```csharp
public class SendPongWhenPinged(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<Pinged>
{
    protected override Task OnHandle(Pinged @event) => commandDispatcher.Send(new PongCommand { Context = ContextOf(@event) });
}
```

`Shared/Application/LoopGuardTests.cs`, through the real dispatcher and pipeline:

@@members variant:advanced/test/Boilerplate.Tests/Shared/Application/LoopGuardTests.cs::LoopGuardTests@@

With an architecture level, the event-contract golden file gains `Chain` on every queue event: run once with `MORTIS_UPDATE_CONTRACTS=1`. `scripts/conformance/loop-guard.sh` checks the chain on the example's log lines.

## Files this add-on changes

`CommandContext.cs`, `EventBase.cs`, `EventContext.cs`, `CommandPipeline.cs`, `LogFields.cs` (using `{Name}.Shared.Application`), `ErrorCatalogue.cs` and the message files when catalogued and translated; with RabbitMQ nothing more, since the envelope carries the whole event.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `LoopGuardConstants.cs` | `{Name}.Shared.Application` | none |
| `LoopDetectedException.cs` | `{Name}.Shared.Exceptions` | `{Name}.Shared.Application`, `System.Net` |
| `PingPongLog.cs` | `{Name}.Tests.TestSupport.Shared.Application` | `System.Text.Json.Serialization`, `{Name}.Shared.Application`, `{Name}.Shared.Domain` |
| `LoopGuardTests.cs` | `{Name}.Tests.Shared.Application` | `{Name}.Shared`, `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `{Name}.Shared.Exceptions`, `{Name}.Shared.Persistence`, `{Name}.Tests.TestSupport.Shared.Application`, `{Name}.Tests.TestSupport.Shared.Persistence`, `Microsoft.Extensions.DependencyInjection` |

## Decision record

`docs/adr/0029-loop-guard.md`, in `project-docs-adr.md`.
