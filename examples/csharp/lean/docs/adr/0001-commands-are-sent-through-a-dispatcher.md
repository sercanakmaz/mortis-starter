# 0001 — A host sends a command through `ICommandDispatcher` and never names its handler

- Status: accepted
- Date: 2026-11-22
- Context: Controllers, the Console and event handlers took the command handlers they needed as constructor arguments (`OrdersController(CreateOrderCommandHandler, GetOrderCommandHandler, ...)`, `CommandScenario<TCommand, TContent, TCommandHandler>`). Every new command meant editing the host's constructor, and a host knew every handler type of the Application layer.

## Decision

A host injects `ICommandDispatcher` and calls `Send(command)`. The command names its result type (`ICommand<OrderDto>`), so `Send` returns `CommandResult<OrderDto>` without the caller naming anything else. Each handler is registered once in `ApplicationModule` with `AddCommandHandler<TCommand, TContent, THandler>()`; the dispatcher resolves `BaseCommandHandler<TCommand, TContent>` from the current scope and calls `Handle`, so validation, middlewares and the transaction run as before.

## What varies

Which handler serves a command. The host must not know.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Inject the handlers into each host | Nothing now; one more constructor argument per command in every host, and three generic arguments per Console scenario | Hosts stay coupled to the Application layer's internals |
| A mediator library | A dependency and, in some libraries, license terms | Forty lines of our own code do the job and validation already lives in `CommandPipeline` |
| **`ICommandDispatcher` with a cached invoker per command type** | `CommandDispatcher`, `CommandHandlerExtensions`, one registration line per command | n/a |

## Consequences

- Gained: a controller has one dependency; a new command needs no host change; the Console runs any command by name.
- Lost or owed: a command with no registered handler fails at its first `Send`, not at start-up. The invoker cache is static and keyed by command type.
- Cost of going back: low. Handlers are unchanged; only the call sites would return to `Handle`.

## How it is enforced

`CommandDispatcherTests` (routing, two kinds of command, no handler, validation failure, null). Review for any host that injects a `*CommandHandler`.
