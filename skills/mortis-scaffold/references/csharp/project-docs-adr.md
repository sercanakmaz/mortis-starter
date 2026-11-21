# Project documents: decision records, C#

Folder `docs/adr/`. Written exactly as shown, replacing `{Name}`; every date is the day the project is generated. `0009` is written only when `addons` contains `correlation`; `0011` only when `events.outbox` is not `none`, with `{outbox}` replaced by its value; `0013` only when `events.transport` is `rabbitmq`. With an outbox, `docs/architecture.md` §3 says "written to `outbox_messages` before COMMIT; `OutboxWorker` runs the event consumers" in place of the `EventPublisher` line, and §8 drops the "no outbox" gap. A value in `MORTIS.md` that differs from its preset gets a further record from `mortis-architect`, numbered after these.

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
~~~~

## 0002-commands-are-records-that-implement-interfaces.md

~~~~markdown
# 0002 — A command is a plain record that implements interfaces

- Status: accepted; the user properties replaced by one context in 0007
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

- Status: accepted; the user properties replaced by one context in 0007
- Date: <the date the project was generated>
- Context: An `IUserContext` service was read deep inside services to stamp audit columns. It was an invisible dependency, it had no value inside an event consumer or the Console, and a test had to fake it.

## Decision

A command that writes implements `IUserCommand`. The presentation layer fills it: `UserCommandFilter` in the Api (claims `NameIdentifier` and `Email`, overwriting anything the client sent) and `ConsoleCommandRunner` in the Console (`console`). The command handler passes `command.ToAuditUser()` as the first argument of the service's write methods, and the service passes it to the aggregate. An event carries the user of the change that raised it: `AggregateRoot` stamps `EventBase.UserId` and `UserEmail` when it raises the event. A sync handler passes `@event.UserId` on to the next command; an event consumer sends it as `SystemUser`.

## What varies

Who the actor is (a person, the console, the system). The domain must not look it up.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| An ambient `IUserContext` | One service, no signature changes | Hidden dependency; wrong or empty in event consumers and the Console |
| Read the claims in each command handler | Nothing in the kernel | Every handler repeats it and can forget it |
| **The user as data on the command, stamped on events** | `AuditUser` first argument of write methods, two properties per writing command | n/a |

## Consequences

- Gained: the actor is visible in every signature and every test; the same command works from the Api, the Console and an event handler.
- Lost or owed: authentication is not configured, so an Api write has an empty audit user until claims exist.
- Cost of going back: high; every write signature carries the user.

## How it is enforced

`UserCommandFilterTests`, `ConsoleCommandRunnerTests`, the aggregate tests for stamping, and rule 20 in `AGENTS.md`.
~~~~

## 0004-event-handlers-live-in-application-and-send-commands.md

~~~~markdown
# 0004 — Event handlers and event consumers live in the Application layer and send commands

- Status: accepted
- Date: <the date the project was generated>
- Context: A reaction to an event (the customer is updated when an order completes, stock moves when a line is added) must run a use case. A handler in the Domain layer would have called services directly and skipped validation, logging and the transaction.

## Decision

A reaction is in the Application layer, next to the commands, and its body is one awaited `commandDispatcher.Send(new ...Command {...})`. A **sync event handler** (`<Action>When<Aggregate><Event>EventHandler : BaseSyncEventHandler<T>`) runs inside the transaction after `SaveChanges` and before the commit; if it throws, the command that raised the event is rolled back. It is registered in `ApplicationModule`. An **event consumer** (`<Action>When<Aggregate><Event>EventConsumer : BaseEventConsumer<T>`) runs after the commit in its own scope and transaction. It is registered with one line in the host that runs it, `AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(3))`, and `Retry(n)` is its number of retries in every publishing model. Nothing is found by scanning.

## What varies

Whether the reaction must succeed together with the change (sync) or may follow it (async).

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Handlers in Domain calling repositories | None | Skips the command pipeline |
| Sync handlers only | One mechanism | A slow or failing side effect would block or roll back the main change |
| **Sync handlers and event consumers, both in Application, both sending commands** | Two base classes, an event publisher and one registration line per consumer | n/a |

## Consequences

- Gained: a reaction gets validation, logging and the transaction like a request does; a sync handler can veto.
- Lost or owed: there is no outbox, so an event consumer is lost if the process dies between the commit and its run; a consumer without its line in the host never runs; nothing stops two reactions triggering each other forever; an event consumer on a contended row needs a concurrency token (`Product` uses `xmin`).
- Cost of going back: moderate.

## How it is enforced

The conformance checks `stock` (sync and async on one event) and `blocked-customer` (a sync veto rolls back), and the example's `*EventHandlerTests` and `*EventConsumerTests`.
~~~~

## 0005-the-console-runs-commands-and-does-not-migrate.md

~~~~markdown
# 0005 — The Console runs one command by name and does not migrate

- Status: accepted
- Date: <the date the project was generated>
- Context: A developer needs to try any command by hand without writing code for it, and applying a migration is a deployment concern, not a testing one.

## Decision

`dotnet run --project src/{Name}.Console -- <Command> '<json>'` runs the command of that name (case-insensitive; a command's name is its type name without `Command`), found in the Application assembly. The JSON argument may be `@file`. The result is printed on stdout as JSON; the log lines go to stderr. The Console user is `console`. There are no scenarios: a flow is several runs. Migrations are applied with `dotnet ef database update`. Exit code 1 means no command name, 2 a failure printed as one line `{code, message, errors}` on stderr.

## What varies

Which commands a developer wants to try by hand.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Fixed scenarios plus a migrate step | Code for every experiment | Every experiment needs code, and migration hides in a test host |
| A registration line per command, plus scenarios | A line per command, scenario classes to keep up | More to maintain than the problem needs |
| **One runner that finds the command by name** | `ConsoleCommandRunner`, a VS Code launch configuration | n/a |

## Consequences

- Gained: any command can be run from VS Code or a script with no Console change; stdout can be piped.
- Lost or owed: a new database needs `dotnet ef database update` first; a multi-step flow is a script of runs.
- Cost of going back: low.

## How it is enforced

`ConsoleCommandRunnerTests` and the Console checks of the conformance suite.
~~~~

## 0006-command-handlers-are-the-use-cases.md

~~~~markdown
# 0006 — Command handlers are the use cases; there is no service per aggregate

- Status: accepted
- Date: <the date the project was generated>
- Context: A service per aggregate (`OrderService`) collected every operation of the aggregate, so each command handler only forwarded one call. The use case was split across two classes, and a change that touched two aggregates had nowhere to live except a service that reached into another aggregate.

## Decision

A command handler is the use case. It loads the aggregates the action needs through their repositories, calls their operations, stamps the user, saves, and maps the result. It may change several aggregates when the action is about all of them; a change that is a consequence of the action is an event handler in the reacting aggregate's folder. A domain service exists only for logic that fits no single aggregate: it needs several entities, or another system behind an anti-corruption port. It is pure by default (the handler passes in what it loaded); it may read through a domain interface, preferably a narrow read port, when the rule decides what data it needs; it never writes: no create, update, upsert or delete, no save, no transaction. The handler persists everything, so one class shows the whole persistence story of a use case.

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

## 0007-one-context-on-the-command.md

~~~~markdown
# 0007 — One context on the command

- Status: accepted
- Date: <the date the project was generated>
- Context: The user was two `[JsonIgnore]` properties on every writing command (0002, 0003). Correlation, causation, tenant and language follow the same path from the host to the events; two more properties per concern on every command does not scale, and an ambient correlation id hid where it came from.

## Decision

A writing command implements `IContextCommand` and declares one property, `Context`, of type `CommandContext` (user, correlation id, causation id, tenant, language). The host fills it; the pipeline completes it (a correlation id when there was none) and tells `EventContext` which command is running, so every event raised until it ends carries its context; a nested command gets its own and the outer one is restored after. An event handler builds its command's context with `ContextOf(@event)`.

## What varies

Which facts about a call travel with it. A new one is a property of `CommandContext`, not of every command.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Two properties per concern on every command | A line per concern per command | Grows with every option |
| An ambient context service | Nothing per command | A hidden dependency, wrong inside event consumers, the problem 0003 removed |
| **One `Context` property, stamped on events by `EventContext`** | `CommandContext`, a stack in `EventContext` | n/a |

## Consequences

- Gained: one line per command; correlation and causation on every event without the handlers doing anything.
- Lost or owed: `EventContext` knows the running command; a command sent outside the pipeline is not stamped.
- Cost of going back: moderate.

## How it is enforced

`ContextCommandTests`, `CommandContextTests`, `EventContextTests`, `CommandPipelineTests`, `verify.sh` (one context line per command), and the conformance check that reads correlation and causation from the `stock-flow` log.
~~~~

## 0008-each-aggregate-chooses-its-key.md

~~~~markdown
# 0008 — Each aggregate chooses its key; an integer key is assigned by the database

- Status: accepted
- Date: <the date the project was generated>
- Context: Most aggregates get a time-ordered `Guid` made in the program, so the id is known before the insert and can be sent by the client. Some need a small integer the database assigns (a person-readable number, an existing system). Such an id does not exist until the insert, which changes how the aggregate raises its first event.

## Decision

`MORTIS.md` `keys.default` gives new aggregates their key type; an aggregate may differ. A `Guid` key is assigned in the constructor and mapped with `ValueGeneratedNever()`. An `int` or `long` key is mapped with `IdentityAggregateConfiguration` (`ValueGeneratedOnAdd()`); the aggregate's constructor raises nothing; the command handler calls `CreateAsync` (which flushes, so the id exists), then the aggregate's `MarkCreated()` raises `Created` with the real id, then `UpdateAsync` hands the event over, all in one transaction. Its create command has no `Id`.

## What varies

Who assigns the id, and so when it is known.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| `Guid` everywhere | None; larger ids | Some aggregates need an integer |
| Integer key with `Created` raised in the constructor | None in code | The event carries `0` |
| **Integer key, `Created` raised by `MarkCreated` after the insert** | One more operation and one more call in the handler | n/a |

## Consequences

- Gained: integer keys where they are needed, with correct events.
- Lost or owed: statement order in the handler is behaviour; the client cannot choose such an id.
- Cost of going back: moderate (the column type and every reference change).

## How it is enforced

`CategoryTests`, `CreateCategoryCommandHandlerTests`, `CategoryConfigurationTests`, and the conformance checks on `POST /categories`.
~~~~

## 0009-correlation-header.md

~~~~markdown
# 0009 — A caller's correlation id is honoured and returned

- Status: accepted
- Date: <the date the project was generated>
- Context: Every command and event carries a correlation id (0007). A caller that already has one (a front end, another service) must be able to pass it in and find every log line of what it caused.

## Decision

`CorrelationIdMiddleware` reads `X-Correlation-Id`; a valid `Guid` is used, anything else is ignored and a new id is made. It puts the id in `HttpContext.Items` for `ContextCommandFilter`, returns it in the response header, and opens a logging scope with it. Command, event and failure log lines carry the correlation id as a structured property.

## What varies

Where the correlation id of a request comes from.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A new id per request | None | A caller cannot follow its own request |
| W3C trace context (OpenTelemetry) | A collector and settings | Comes with the observability add-on |
| **The `X-Correlation-Id` header, echoed** | One middleware | n/a |

## Consequences

- Gained: one id from the caller through every command, event and log line.
- Lost or owed: a caller can choose the id; it is a label, never a security boundary.
- Cost of going back: low.

## How it is enforced

`CorrelationIdMiddlewareTests` and the conformance checks of the correlation add-on.
~~~~

## 0010-one-error-body.md

~~~~markdown
# 0010 — One error body, the same at every level

- Status: accepted
- Date: <the date the project was generated>
- Context: Clients branch on errors. Moving a project from a simple to a fuller error handling must not change what a client receives for the same failure.

## Decision

This project uses the `<exceptions value from MORTIS.md>` level. Every failure answers `{ code, message, errors }` with a fixed status: 404 not found, 422 validation (with `errors` per field), 409 business rule, 500 unexpected (with a reference, never the exception's message). At `coded` and above, a unique index answers 409 `duplicate` and a foreign key 400 `reference_violation`; at `minimal` both are unexpected. At `catalogued`, every code is declared once with its default message in `ErrorCatalogue`. 4xx is logged as a warning, 5xx as an error.

## What varies

How much a failure says, never its shape or its status.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A body and status per level | Nothing extra | Moving up a level breaks clients |
| Problem details (RFC 9457) | Framework support | One shape across the Api and the Console was simpler |
| **One body, fixed statuses, levels add detail** | The table in `docs/architecture.md` | n/a |

## Consequences

- Gained: clients are never broken by a change of level; unexpected failures leak nothing and can be found by reference.
- Lost or owed: at `minimal` a duplicate is a 500 until the level moves up.
- Cost of going back: low.

## How it is enforced

`ExceptionHandlerTests`, `ErrorCatalogueTests` at `catalogued`, `verify.sh` (rule codes are constants), and the conformance table of failures.
~~~~

## 0011-outbox-and-publishing-models.md

~~~~markdown
# 0011 — Events that leave the transaction go through an outbox

- Status: accepted
- Date: <the date the project was generated>
- Context: An event consumer runs after the command's transaction has committed (0004). If the process dies in between, the event is lost and the change it should have caused never happens. This project must not lose events.

## Decision

Every queue event is written to `outbox_messages` by the unit of work, in the command's own transaction, after the sync handlers have run: one row per registered event consumer, because every event consumer is a consumer of its own. `OutboxWorker`, a background service, takes the next due row with `for update skip locked`, runs that row's consumer and deletes the row, all in one transaction. A failure rolls back, counts the attempt and waits `2^attempts` seconds; the row's attempt limit is its consumer's `Retry(n)` + 1, and a row that reaches it stays for an operator.

This project publishes `{outbox}`:

- `background` (publish in the command): the command only writes rows; the worker publishes them within a polling interval.
- `after-command` (publish after the command): right after `COMMIT` the request publishes its own rows; the worker sweeps and retries what is left.

Only the chosen mode's code is in the project.

## What varies

When an event is published after the change that raised it.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Publish in process after `COMMIT` (`none`) | Nothing | An event is lost when the process dies before its consumers run |
| Publish in the transaction, before `COMMIT` | Nothing | A rolled-back command would have published its events |
| An outbox plus an inbox and a separate send step | A second table, a lock column, idempotent handlers | More moving parts than one transaction per row |
| **One table; the row, its consumer and its deletion in one transaction** | One table, a worker host | n/a |

## Consequences

- Gained: "the change happened" and "the event will be handled" are one fact; a row is handled once or not at all.
- Lost or owed: a slow handler holds its row's lock; a retry can reorder two events; renaming an event type strands its pending rows until they are published.
- Cost of going back: medium; the table and the worker go, the handlers stay.

## How it is enforced

`OutboxMessageTests`, and the conformance checks in `outbox.sh`: atomicity, deletion, failure, context and latency.
~~~~

## 0012-json-log-lines.md

~~~~markdown
# 0012 — Every log line is one flat JSON object, CorrelationId first

- Status: accepted
- Date: <the date the project was generated>
- Context: Log lines are read by machines first (search, alerts, the conformance suite) and must tie every line of one request, command or event together. Text lines need a parser per format; nested JSON needs a query per level.

## Decision

Every host logs through `JsonLineFormatter`: one JSON object per line, no nesting. `CorrelationId` is the first field, then `Timestamp`, `Level`, `Service`, `Version`, `Category`, `Message`, then every field of the open logging scopes and of the message template, then `Exception`. The command pipeline opens a scope with `CorrelationId`, `CausationId`, `TenantId`, `UserId` and `Command`; publishing or handling an event opens one with the event's fields and `Event`, `EventId`. Messages are templates, never built strings. The Console writes its log lines to stderr, so stdout holds only the result.

## What varies

Nothing per project: every environment logs the same way.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Text lines | Nothing | Every reader needs its own parser |
| The built-in JSON console formatter | Nothing | Scope fields are nested in a `Scopes` list, not at the top level |
| A logging library with enrichers and sinks | A dependency to pin and teach | More than one flat line needs |
| **One small formatter class** | `JsonLineFormatter`, `LogFields` | n/a |

## Consequences

- Gained: any line can be found by `CorrelationId` with one field query; the conformance suite reads fields, not text.
- Lost or owed: lines are harder to read by eye in a terminal.
- Cost of going back: low.

## How it is enforced

`JsonLineFormatterTests`, and the log checks of `example-console.sh`.
~~~~

## 0013-own-bus-compatible-with-masstransit.md

~~~~markdown
# 0013 — Events leave the service on our own RabbitMQ bus, wire-compatible with MassTransit 8

- Status: accepted
- Date: <the date the project was generated>
- Context: Other services must hear of this service's events, and some of them use MassTransit. MassTransit 9 needs a licence and MassTransit 8 leaves maintenance after 2026; the starter uses a fraction of it (publish, consume, retry, error queue).

## Decision

`OutboxWorker` publishes each outbox row to RabbitMQ with `RabbitMQ.Client` and deletes the row only after the broker confirmed it. The message is a MassTransit 8 envelope (`application/vnd.masstransit+json`, camelCase, `messageType` URNs of the type, its base types and its own interfaces) on a durable fanout exchange named `<Namespace>:<TypeName>`.

Every event consumer is a consumer of its own. The Worker declares, in code, one receive endpoint per consumer, one line each (`AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))`): a classic durable queue named after the consumer (`decrease-stock-when-order-line-added`, or `Named(queue)`), an exchange of the same name bound to the consumer's event exchange. Each message runs the consumer in its own transaction; one consumer's failure never touches another's queue. The broker delivers at least once and the bus does not deduplicate: every event consumer must be safe to run twice. After the retries, the message goes to `<queue>_error` with the `MT-Fault-*` headers. A message no consumer wants is acknowledged and logged.

Envelopes written by MassTransit with System.Text.Json or Newtonsoft are read; ours are written in one format. No MassTransit package is referenced by the project.

## What varies

Which broker carries events between services, and what other services must understand.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| MassTransit 9 | A licence | Cannot be assumed for a starter others install |
| MassTransit 8 | Free | Maintenance ends after 2026; wrong foundation for new services |
| **Our own bus with the MassTransit 8 wire format** | Nine small classes, the interoperability tests | n/a |

## Consequences

- Gained: MassTransit services and ours exchange events both ways; a Go service can write the same format.
- Lost or owed: request/response, sagas, scheduling, delayed redelivery and `Fault<T>` events are not provided; an endpoint's configuration lives in code; a redelivered message runs its consumer again, so consumers must be safe to run twice.
- Cost of going back: medium; the handlers stay, the transport classes are replaced.

## How it is enforced

`MessageNamesTests`, `MessageEnvelopeTests` (including envelopes MassTransit wrote), the conformance checks in `transport-rabbitmq.sh`, and the interoperability tests run against MassTransit 8 while the skills are built.
~~~~
