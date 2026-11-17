# Project documents: decision records, C#

Folder `docs/adr/`. Written exactly as shown, replacing `{Name}`; every date is the day the project is generated. A value in `MORTIS.md` that differs from its preset gets a further record from `mortis-architect`, numbered after these.

## 0000-template.md

~~~~markdown
# NNNN — Title: the decision in one sentence

- Status: proposed | accepted | replaced by NNNN
- Date: YYYY-MM-DD
- Context: what happened that made this a decision. Name the files and, where you have them, the numbers.

## Decision

What is done from now on. Write it as a rule someone can follow without reading the rest.

## What varies

The axis this decision is about ("which carrier ships the order"), or "nothing: this is a placement decision".

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| The cheap one | files touched; what it costs when the next case arrives | |
| The structural one | files touched; what it costs now | |
| **Chosen** | | n/a |

## Consequences

- Gained:
- Lost or owed:
- Cost of going back:

## How it is enforced

A test, a build rule, a check in the review script, or "review only".
~~~~

## 0001-commands-are-sent-through-a-dispatcher.md

~~~~markdown
# 0001 — A host sends a command through `ICommandDispatcher` and never names its handler

- Status: accepted
- Date: <the date the project was generated>
- Context: Controllers, Console scenarios and event handlers took the command handlers they needed as constructor arguments (`OrdersController(CreateOrderCommandHandler, GetOrderCommandHandler, ...)`, `CommandScenario<TCommand, TContent, TCommandHandler>`). Every new command meant editing the host's constructor, and a host knew every handler type of the Application layer.

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

- Gained: a controller or scenario has one dependency; a new command needs no host change; the Console runs any registered command by name.
- Lost or owed: a command with no registered handler fails at its first `Send`, not at start-up. The invoker cache is static and keyed by command type.
- Cost of going back: low. Handlers are unchanged; only the call sites would return to `Handle`.

## How it is enforced

`CommandDispatcherTests` (routing, two kinds of command, no handler, validation failure, null). Review for any host that injects a `*CommandHandler`.
~~~~

## 0002-commands-are-records-that-implement-interfaces.md

~~~~markdown
# 0002 — A command is a plain record that implements interfaces

- Status: accepted
- Date: <the date the project was generated>
- Context: `BaseCommand<TContent>` and `UserCommand<TContent>` were abstract records. They carried no behaviour a marker could not carry, except two `[JsonIgnore]` properties and `ToAuditUser()`, and they used up the command's one base type.

## Decision

`ICommand<TContent>` says "I am a command and my result is `TContent`". `IUserCommand` (`UserId`, `UserEmail`) says "the host fills the user in". `ITransactionalCommand` says "run me in a transaction". A command implements the ones it needs: `ICommand<ProductDto>, IUserCommand, ITransactionalCommand` when it writes, `ICommand<ProductDto>` when it only reads. `ToAuditUser()` is an extension method on `IUserCommand`.

## What varies

Nothing: this is a placement decision.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Abstract base records | The two `[JsonIgnore]` properties are written once | A command could not derive from anything else, and reads and writes sat in one hierarchy for no behavioural reason |
| **Interfaces only** | Each writing command declares the two `[JsonIgnore]` properties itself (an attribute on an interface member is not inherited by the implementation) | n/a |
| Interfaces plus one `AuditUser User` property | One line per command instead of two | Not done yet: it also changes the filter, the Console, event stamping and tests. It is the next step if the repetition hurts |

## Consequences

- Gained: no inheritance in the command model; queries and writes differ only in the interfaces they list.
- Lost or owed: two repeated lines in every writing command.
- Cost of going back: moderate; every command declaration changes.

## How it is enforced

`UserCommandTests` (a `userId` in the JSON body is ignored), the compiler, and `ICommandDispatcher.Send` accepting only `ICommand<TContent>`.
~~~~

## 0003-the-user-is-data-on-the-command.md

~~~~markdown
# 0003 — The user is data on the command, never an ambient service

- Status: accepted
- Date: <the date the project was generated>
- Context: An `IUserContext` service was read deep inside services to stamp audit columns. It was an invisible dependency, it had no value inside an async event handler or the Console, and a test had to fake it.

## Decision

A command that writes implements `IUserCommand`. The presentation layer fills it: `UserCommandFilter` in the Api (claims `NameIdentifier` and `Email`, overwriting anything the client sent) and `CommandScenario` / `.AsConsoleUser()` in the Console (`console`). The command handler passes `command.ToAuditUser()` as the first argument of the service's write methods, and the service passes it to the aggregate. An event carries the user of the change that raised it: `AggregateRoot` stamps `EventBase.UserId` and `UserEmail` when it raises the event. A sync handler passes `@event.UserId` on to the next command; an async handler sends it as `SystemUser`.

## What varies

Who the actor is (a person, the console, the system). The domain must not look it up.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| An ambient `IUserContext` | One service, no signature changes | Hidden dependency; wrong or empty in async handlers and the Console |
| Read the claims in each command handler | Nothing in the kernel | Every handler repeats it and can forget it |
| **The user as data on the command, stamped on events** | `AuditUser` first argument of write methods, two properties per writing command | n/a |

## Consequences

- Gained: the actor is visible in every signature and every test; the same command works from the Api, the Console and an event handler.
- Lost or owed: authentication is not configured, so an Api write has an empty audit user until claims exist.
- Cost of going back: high; every write signature carries the user.

## How it is enforced

`UserCommandFilterTests`, `CommandScenarioTests`, the aggregate tests for stamping, and rule 20 in `AGENTS.md`.
~~~~

## 0004-event-handlers-live-in-application-and-send-commands.md

~~~~markdown
# 0004 — Event handlers live in the Application layer and send commands; sync and async are separate

- Status: accepted
- Date: <the date the project was generated>
- Context: A reaction to an event (the customer is updated when an order completes, stock moves when a line is added) must run a use case. A handler in the Domain layer would have called services directly and skipped validation, logging and the transaction.

## Decision

An event handler is in the Application layer, next to the commands, named `<Action>When<Aggregate><Event>EventHandler`. Its body is one awaited `commandDispatcher.Send(new ...Command {...})`. A **sync** handler (`BaseSyncEventHandler<T>`) runs inside the transaction after `SaveChanges` and before the commit; if it throws, the command that raised the event is rolled back. An **async** handler (`BaseAsyncEventHandler<T>`) runs after the commit in a new scope and a new transaction, and is retried on `DbUpdateConcurrencyException` (up to ten attempts). Registration is explicit in `ApplicationModule`.

## What varies

Whether the reaction must succeed together with the change (sync) or may follow it (async).

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Handlers in Domain calling repositories | None | Skips the command pipeline |
| Sync handlers only | One mechanism | A slow or failing side effect would block or roll back the main change |
| **Sync and async, both in Application, both sending commands** | Two base classes and an in-process dispatcher | n/a |

## Consequences

- Gained: a reaction gets validation, logging and the transaction like a request does; a sync handler can veto.
- Lost or owed: there is no outbox, so an async handler is lost if the process dies between the commit and its run; nothing stops two handlers triggering each other forever; an async handler on a contended row needs a concurrency token (`Product` uses `xmin`).
- Cost of going back: moderate.

## How it is enforced

The Console scenarios `stock-flow` (sync and async on one event) and `blocked-customer` (a sync veto rolls back), and the example's `*EventHandlerTests`.
~~~~

## 0005-the-console-runs-commands-and-does-not-migrate.md

~~~~markdown
# 0005 — The Console runs commands by name and does not migrate

- Status: accepted
- Date: <the date the project was generated>
- Context: The Console migrated the database and ran a fixed set of scenarios. Trying a new command meant writing a scenario first, and applying a migration is a deployment concern, not a testing one.

## Decision

`dotnet run --project src/{Name}.Console -- <Name> '<json>'` runs the scenario or the command of that name (case-insensitive; a command's name is its type name without `Command`). Which commands the Console can run is decided by one line each in `ScenarioModule`: `AddCommandScenario<TCommand, TContent>()`. The JSON argument may be `@file`. The Console user is `console`, and async handlers are awaited before the process exits. Migrations are applied with `dotnet ef database update`. Exit code 1 means an unknown name, 2 a domain error printed as `{code, message, errors}` on stderr.

## What varies

Which commands a developer wants to try by hand.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Fixed scenarios plus a migrate step | Nothing new | Every experiment needs code, and migration hides in a test host |
| **A generic command runner plus scenarios** | `CommandScenario`, one registration line per command, a VS Code launch configuration | n/a |

## Consequences

- Gained: any command can be run from VS Code with arguments; a flow that spans several commands stays a scenario.
- Lost or owed: a new database needs `dotnet ef database update` first.
- Cost of going back: low.

## How it is enforced

`CommandScenarioTests` and the Console checks of the conformance suite.
~~~~

## 0006-command-handlers-are-the-use-cases.md

~~~~markdown
# 0006 — Command handlers are the use cases; there is no service per aggregate

- Status: accepted
- Date: <the date the project was generated>
- Context: A service per aggregate (`OrderService`) collected every operation of the aggregate, so each command handler only forwarded one call. The use case was split across two classes, and a change that touched two aggregates had nowhere to live except a service that reached into another aggregate.

## Decision

A command handler is the use case. It loads the aggregates the action needs through their repositories, calls their operations, stamps the user, saves, and maps the result. It may change several aggregates when the action is about all of them; a change that is a consequence of the action is an event handler in the reacting aggregate's folder. A domain service exists only for logic that fits no single aggregate: it needs several entities, or another system behind an anti-corruption port. It reads and decides; the handler saves.

## What varies

Whether a second change is the intent of the action or a consequence of it. The test: without the second change, does the command still do what its name says?

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A service per aggregate, handlers call one method | One more class per aggregate and a forwarding handler | The use case is in two places; cross-aggregate actions have no home |
| **Handlers are use cases, domain services only when needed** | Handlers hold the load-call-save steps | n/a |

## Consequences

- Gained: one place per use case; cross-aggregate actions that must be atomic have a home; domain services are small and named for their concept.
- Lost or owed: reviewers must apply the intent-or-consequence test; a handler that grows a line per new side effect is the smell to watch.
- Cost of going back: moderate.

## How it is enforced

`AGENTS.md` rules 8 to 10, and the `mortis-review` skill.
~~~~
