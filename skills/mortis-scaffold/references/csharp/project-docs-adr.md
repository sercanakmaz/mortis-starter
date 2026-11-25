# Project documents: decision records, C#

Folder `docs/adr/`. Written exactly as shown, replacing every placeholder: `{Name}` with the `name` row, `<the date the project was generated>` with the day the project is generated, `<exceptions value from MORTIS.md>` with the `exceptions` row, `{testing}` with the `testing` row. `0009` is written only when `addons` contains `correlation`; `0011` only when `events.outbox` is not `none`, with `{outbox}` replaced by its value; `0013` only when `events.transport` is `rabbitmq`; `0014` only when `auth` is `jwt`; `0015` only when `tenancy.mode` is `interface`; `0016` only when `languages.mode` is not `none`; `0017` only when `database` is `mongo` (not built yet, decision 45); `0018` only when `queries` is not `commands` (not built yet, decision 46); `0019` only when `auth` is `jwt` (not built yet, decision 47); `0020` only when `addons` contains `api-conventions`, `0021` only when it contains `idempotent-commands` and `database` is `postgres`, `0022` only when it contains `observability` (not built yet, decision 48); `0023` only when `testing` is not `unit` (not built yet, decision 49); `0024` to `0031` only when `addons` contains, in order, `soft-delete`, `caching`, `scheduler` (with PostgreSQL), `storage`, `secrets` (with PostgreSQL), `loop-guard`, `contexts`, `events-project` (not built yet, decision 50). With an outbox, `docs/architecture.md` §3 says "written to `outbox_messages` before COMMIT; `OutboxWorker` runs the event consumers" in place of the `EventPublisher` line, and §8 drops the "no outbox" gap. A value in `MORTIS.md` that differs from its preset gets a further record from `mortis-architect`, numbered after these.

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

A command that writes implements `IUserCommand`. The presentation layer fills it: `UserCommandFilter` in the Api (claims `NameIdentifier` and `Email`, overwriting anything the client sent) and `ConsoleArguments` in the Console (the run's `--user` and `--email`). The command handler passes `command.ToAuditUser()` as the first argument of the service's write methods, and the service passes it to the aggregate. An event carries the user of the change that raised it: `AggregateRoot` stamps `EventBase.UserId` and `UserEmail` when it raises the event. A sync handler and an event consumer both pass `@event.UserId` on to the next command; there is no system user.

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

A reaction is in the Application layer, next to the commands, and its body is one awaited `commandDispatcher.Send(new ...Command {...})`. A **sync event handler** (`<Action>When<Aggregate><Event>EventHandler : BaseSyncEventHandler<T>`) runs inside the transaction after `SaveChanges` and before the commit; if it throws, the command that raised the event is rolled back. It is registered in `ApplicationModule`. An **event consumer** (`<Action>When<Aggregate><Event>EventConsumer : BaseEventConsumer<T>`) runs after the commit in its own scope and transaction. It is registered with one line in the host that runs it, `AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(10))`, and `Retry(n)` is its number of retries in every publishing model. Nothing is found by scanning.

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

`dotnet run --project src/{Name}.Console -- --user <id> <Command> '<json>'` runs the command of that name (case-insensitive; a command's name is its type name without `Command`), found in the Application assembly. The JSON argument may be `@file`. The result is printed on stdout as JSON; the log lines go to stderr. The run names its user with `--user <id>` (required) and `--email`, and its tenant and language with `--tenant` and `--language` where those options are on; the Console takes no token. There are no scenarios: a flow is several runs. Migrations are applied with `dotnet ef database update`. Exit code 1 means no command name, 2 a failure printed as one line `{code, message, errors}` on stderr.

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

Every event consumer is a consumer of its own. The Worker declares, in code, one receive endpoint per consumer, one line each (`AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))`): a classic durable queue named after the consumer (`decrease-stock-when-order-line-added`, or `Named(queue)`), an exchange of the same name bound to the consumer's event exchange. Each message runs the consumer in its own transaction; one consumer's failure never touches another's queue. The broker delivers at least once and the bus does not deduplicate: every event consumer must be safe to run twice. After the retries, the message goes to `<queue>_error` with the `MT-Fault-*` headers. A message no consumer wants is acknowledged and logged.

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

## 0014-bearer-tokens-fill-the-context.md

~~~~markdown
# 0014 — Callers present bearer tokens; their claims fill the command's context

- Status: accepted
- Date: <the date the project was generated>
- Context: Every write records who made it, and with tenancy every row records whose it is. Before this, nothing authenticated the Api, so the audit user of a write was empty. The project must not become an identity provider.

## Decision

The Api validates JWT bearer tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`) and every controller needs an authenticated caller; `/health` stays open. Keys come from an OpenID Connect authority (`Auth:Authority`) or, in Development only, a symmetric key (`Auth:SigningKey` in `appsettings.Development.json`). `ContextCommandFilter` fills `CommandContext.User` from the subject and email claims, and the tenant from the tenant claim when tenancy is on; the claim names are settings. The Console takes no token: tokens belong to the HTTP layer, and a Console run names its user with `--user`. `dotnet run -- dev-token` prints a development token for HTTP calls and refuses outside Development. Event consumers run as the user whose command raised the event; there is no system user.

## What varies

Which identity provider issues the tokens, and what it names the claims.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| No authentication | Nothing | Writes have no audit user; tenants cannot be told apart |
| Issue our own tokens | A user store, passwords, refresh tokens | Not this service's job |
| An API key per caller | A key table and rotation | Does not identify the user behind a call |
| **Validate tokens from an authority; claims fill the context** | One package, settings, a development key | n/a |

## Consequences

- Gained: every write records the real user, including the writes of event consumers; local work needs no identity server.
- Lost or owed: permissions per command are not checked yet; the development key must never reach another environment.
- Cost of going back: low.

## How it is enforced

`AuthSettingsTests`, `ConsoleTokensTests`, `ContextCommandFilterTests`, and `auth.sh` (401 without or with a forged token, audit columns from the token, `dev-token` refused outside Development).
~~~~

## 0015-multi-tenancy-by-interface.md

~~~~markdown
# 0015 — Tenants share one database; a type is tenant-owned by implementing IMultiTenant

- Status: accepted
- Date: <the date the project was generated>
- Context: The service is sold to many customers, and one customer must never see another's data. A database or schema per tenant multiplies migrations and connections, which is too much for a starter.

## Decision

One database for every tenant. A type that implements `IMultiTenant` has a `tenant_id` column; everything else is global. The tenant is the signed-in user's tenant claim (`0014`); the host enters it in a scoped `TenantScope`, the consumers enter the event's tenant before resolving any service, and the command pipeline checks the command's context against it. `AppDbContext` filters every tenant-owned query by the scope's tenant and stamps new rows with it; with no tenant, a tenant-owned read or write fails with 403 `tenant_required` before any SQL. Every unique index and foreign key between tenant-owned tables starts with `tenant_id`. `TenantScope.Bypass` is the only way past the filter, for system jobs, and it is logged.

## What varies

Which types belong to a tenant.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A database or schema per tenant | Migrations and connections per tenant | Too exceptional for a starter |
| A tenant condition written in every query | Nothing up front | One forgotten condition leaks data |
| PostgreSQL row-level security only | Policies per table and a session variable | Harder to test; kept as optional hardening |
| **One interface, a query filter and a scoped tenant** | `TenantScope`, a middleware, tenant-first keys | n/a |

## Consequences

- Gained: a type becomes tenant-owned by one interface; a missing tenant fails loudly; keys stop cross-tenant references in the database.
- Lost or owed: raw SQL and bulk updates skip the filter and need the condition by hand; the scope is ambient, so a job must enter the tenant before it resolves a repository.
- Cost of going back: high, because the data is already mixed in one database.

## How it is enforced

`TenantScopeTests`, `TenantCommandHandlerMiddlewareTests`, `AppDbContextTests`, the configuration tests, and `tenancy.sh` (isolation over HTTP with two tenants, interleaved consumers, the cross-tenant foreign key, tenant-first unique indexes).
~~~~

## 0016-languages-of-messages-and-data.md

~~~~markdown
# 0016 — Messages answer in the caller's language; translated data is stored per language

- Status: accepted
- Date: <the date the project was generated>
- Context: Users of this service speak more than one language. They should read failures in their own language, and some business data (a product's name) exists in several languages.

## Decision

The language of a call is the signed-in user's language claim, then the request's `Accept-Language`, limited to the supported languages with the default as the fallback; the Console takes the claim of its token. It travels on `CommandContext.Language` and on every event. A failure carries a code and its parameters; the answer's message is looked up at the edge in `messages.<language>.json` and falls back to the default language. A translated field is a `LocalizedText` value object stored as one `jsonb` column: a write stores one text under the caller's language, a read returns the caller's language (falling back to the default) and every translation. The supported languages are generated constants.

## What varies

Which languages are supported, and which fields are translated.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Messages in the code, one language | Nothing | Users read failures in a language not theirs |
| .NET `.resx` resources | Designer files, culture-named assemblies | Harder to edit and review than one JSON file per language |
| A table per translated field | A join per read | More schema than one JSON column |
| **Codes with parameters, a JSON file per language, `LocalizedText` in jsonb** | Two small classes, a converter | n/a |

## Consequences

- Gained: one failure answers in each user's language; a name is stored once per language; a consumer runs in the language of the request that caused it.
- Lost or owed: field messages of validation failures are not translated; searching by a translated field needs its JSON path and, for speed, an index per language.
- Cost of going back: moderate.

## How it is enforced

`MessageLocalizerTests` (every code has a text in every language), `LanguagesTests`, `LocalizedTextTests`, `ExceptionHandlerTests`, and `languages.sh`.
~~~~

## 0017-aggregates-as-documents-in-mongodb.md

~~~~markdown
# 0017 — Aggregates are documents in MongoDB

- Status: accepted
- Date: <the date the project was generated>
- Context: The aggregates fit one document each, and the team does not want a relational mapping or migrations. The outbox and the units of work still need atomic writes across documents.

## Decision

Each aggregate is one document, with its child entities and value objects embedded; references between aggregates are ids. Class maps in Infrastructure keep the Domain free of driver attributes. `MongoRepository` implements the same `IRepository`: a `version` number on each document, checked on every replace, gives optimistic concurrency. The unit of work is a client session with a multi-document transaction, so the server runs as a replica set (a single node in development). The outbox is a collection written in the same session. Unique indexes replace unique constraints; they are ensured at start in Development and by `dotnet run -- ensure-indexes` in deployment. Integer keys come from a counter collection only where the Domain fixes them; `keys.default` stays `guid`.

## What varies

Which fields are indexed, and how a document's shape changes over time.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| PostgreSQL with EF Core | Migrations and a relational mapping | The team chose documents |
| MongoDB without transactions | Nothing to configure | The outbox and the command could disagree after a crash |
| **MongoDB with a replica set and session transactions** | A replica set, a version field, class maps | n/a |

## Consequences

- Gained: no migrations; an aggregate is read and written in one operation; the Domain and Application code is the same as with PostgreSQL.
- Lost or owed: no foreign keys, so a reference to a missing aggregate is not refused by the database; a standalone server cannot run the project; a document shape change needs an upgrade on read.
- Cost of going back: high, because the data must be moved.

## How it is enforced

`MongoClassMapsTests` (standard UUIDs, embedded lines), `MongoRepositoryRulesTests` (every query through the repository base), and `database-mongo.sh` (the example's flows read back from the documents, rollback, concurrent updates, duplicates).
~~~~

## 0018-queries-and-read-models.md

~~~~markdown
# 0018 — Reads have their own types; screens that need it get a read model

- Status: accepted
- Date: <the date the project was generated>
- Context: List screens need paging, filters and sorting, and loading aggregates to map them is slow and couples reads to the write model. Some screens need data from several aggregates at once.

## Decision

A read is a query: `IQuery<TResult>` with the caller's context, a `BaseQueryHandler` with an optional validator, and an `IQueryDispatcher` the hosts use. A query handler reads through a reader port declared next to the repository in Domain, implemented in Infrastructure as a projection to a view record: no tracking, filter, sort and page in the database. List queries have typed fields and a closed sort set; `PageRequest` and `PagedResult<T>` are in Shared. With read models, a table per screen (the example's `order_summaries`) is refreshed by sync event handlers inside the command's transaction, each recomputing the whole row, and a Console command rebuilds it.

## What varies

Which screens need a list query, and which need a read model.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Reads as commands that load aggregates | Nothing new | List screens load whole aggregates; no paging in the database |
| A generic filter language | A parser and a closed operator set | Typed fields per query are simpler to validate and index |
| Read models updated by event consumers | An outbox, eventual consistency | The example's screen needs to match the write at once |
| **Queries with reader ports; read models refreshed by sync handlers** | Query types, readers, a table and its handlers | n/a |

## Consequences

- Gained: lists page and filter in SQL; reads never touch the unit of work; a read model is consistent with the write and can be rebuilt.
- Lost or owed: a reader and view records per aggregate that is read; data copied into a read model from another aggregate is refreshed only by its own aggregate's events.
- Cost of going back: low.

## How it is enforced

`QueryRulesTests` (no query handler depends on the unit of work, a repository or a domain service), `ProductReaderTests` (the page's SQL), `ReadModelModuleTests`, and `queries.sh`.
~~~~

## 0019-every-command-declares-a-permission.md

~~~~markdown
# 0019 — Every command declares a permission, checked against the caller's

- Status: accepted
- Date: <the date the project was generated>
- Context: Signed-in callers (`0014`) may still be allowed to do only part of what the service offers. Leaving a command unprotected by mistake must be impossible, and a reaction to an event must not do more than the user who caused it may.

## Decision

Every command implements `IAuthorizedCommand` with one permission constant, declared per aggregate next to the commands; an architecture test fails otherwise, and there is no allow-list. The caller's permissions come from a claim whose name and form the developer chose (`Auth:PermissionsClaim`, `Auth:PermissionsFormat`) and travel on the context and on every event. `CommandPipeline` checks the command's permission before validation and answers 403 `forbidden`. There is no system user and no bypass: a command sent by an event handler or consumer runs as the user whose command raised the event, with that user's permissions. The Console names the run's permissions with `--permissions`.

## What varies

Which permissions exist and who holds them; the identity provider decides the second.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Authenticated is enough | Nothing | Every signed-in caller can do everything |
| An allow-list of open commands | One list to keep | A forgotten command is open by default |
| Reactions as a system user that skips checks | A trusted actor | Audit loses the real user; a reaction may do more than its cause |
| **One permission per command, checked for every caller and every reaction** | A constant per command; reaction permissions granted with their triggers | n/a |

## Consequences

- Gained: no command is open by mistake; audit shows the real user of every write, reactions included; a refusal happens before the request's shape is checked.
- Lost or owed: a user needs the permissions of the reactions their commands trigger (creating an order needs `products.decrease-stock`); permissions travel with events, so the outbox and the bus carry them.
- Cost of going back: low.

## How it is enforced

`PermissionRulesTests`, the pipeline tests, and `permissions.sh` (403 without the permission, before validation; reactions as the user; the Console's `--permissions`).
~~~~

## 0020-one-shape-for-requests-and-failures.md

~~~~markdown
# 0020 — One shape for requests, lists and failures

- Status: accepted
- Date: <the date the project was generated>
- Context: A front end or another team consumes this API. Each framework failure (a body that does not parse, an unknown route) has its own default shape, a typo in a request member is silently ignored, and a partial update cannot tell "leave it" from "clear it".

## Decision

Every failure answers the error body `{ code, message, errors? }`, the framework's own included: `invalid_request` for a body that does not parse or names a member the command does not have, `route_not_found` and `method_not_allowed` for an unknown address. Unknown members are refused by default; a command that must accept them says so on its own type with `[JsonUnmappedMemberHandling(Skip)]`. `PUT` replaces; `PATCH` changes only the members present, which are `Optional<T>` so an absent member and `null` differ. Enums travel as their names. A list answers `{ items, page, size, total }`. There is one OpenAPI document and no API versioning.

## What varies

Which endpoints exist and which commands accept extra members.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| The framework's defaults | Nothing | Three error shapes; typos ignored; `null` and absent are the same |
| ProblemDetails (RFC 9457) everywhere | A second body shape next to the one commands already answer | Clients would parse both during the change |
| JSON Patch for partial updates | A patch document per request | Hard to validate against a command; clients rarely produce it |
| URL versioning (`/v1/`) from the start | A version segment and a document per version | No consumer needs a second version yet; added when a breaking change comes |
| **One error body, unknown members refused, `Optional<T>` for PATCH, no versioning** | A converter and a few conventions | n/a |

## Consequences

- Gained: a client parses one failure shape; a typo fails loudly with the member's name; `PATCH` can clear a member.
- Lost or owed: a client that sends extra members must stop or its command must opt out; the first breaking change needs a versioning decision.
- Cost of going back: low.

## How it is enforced

`ApiConventionsModuleTests`, `OptionalTests`, the `PATCH` handler tests, and `api-conventions.sh` (an unknown member is a 400 naming it and accepted by the opted-out command; an unknown route and a broken body answer the error body; `PATCH` with `null` clears and `{}` keeps).
~~~~

## 0021-idempotency-keys-for-post.md

~~~~markdown
# 0021 — A retried POST with the same idempotency key runs once

- Status: accepted
- Date: <the date the project was generated>
- Context: Clients retry a `POST` when a response is lost (a mobile network, a timeout, a queue that redelivers). Without help, each retry creates another order.

## Decision

A `POST` with an `Idempotency-Key` header is recorded in `idempotency_records`, scoped by the caller's tenant and user, with a hash of its method, path and body. A repeat with the same key and body gets the first response again, with `Idempotency-Replayed: true`; the same key with another body is a 422 `idempotency_key_reused`; a repeat while the first still runs is a 409 `idempotency_in_progress`. Only successes are stored; a failure frees the key. Records are deleted after `Idempotency:RetentionHours` (24). A `POST` without the header runs as before.

## What varies

Which clients send the header, and the retention period.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Rely on unique business keys (an order number) | Nothing extra | Only commands with a natural key are protected, and the retry gets an error instead of the first answer |
| Store the key in the command's transaction | The filter would have to live inside the pipeline | HTTP concerns would enter the application layer |
| **A resource filter and a table of keys with their responses** | One table, a cleanup, a small window after a crash | n/a |

## Consequences

- Gained: a client may retry any `POST` safely and gets the same answer.
- Lost or owed: a crash after the command committed and before the response was stored lets a retry run again after `Idempotency:InProgressTimeoutSeconds`; the command's own unique rules are the last guard. Responses are kept for the retention period.
- Cost of going back: low.

## How it is enforced

`IdempotencyFilterTests`, `IdempotencyStoreTests`, and `idempotency.sh` (a repeat returns the first response and creates one row; another body with the same key is a 422).
~~~~

## 0022-health-traces-and-metrics.md

~~~~markdown
# 0022 — Health, one trace per request across the outbox and the bus, and metrics

- Status: accepted
- Date: <the date the project was generated>
- Context: In production someone must see whether the service can serve, why a request was slow, and what happened to the events it raised, after the outbox and the bus have split the work across processes and time.

## Decision

`/health/live` answers while the process runs; `/health/ready` checks the database, the broker when there is one, and that the oldest unpublished outbox row is younger than `Observability:OutboxLagThresholdSeconds`. Spans come from one `ActivitySource` named after the project: one per command, sync handler, outbox publish and consumed event, under the HTTP request span. The W3C `traceparent` is stored with the outbox row and sent in the bus envelope headers, so one trace covers a request and everything it caused, with every transport. Metrics: command duration by command and outcome, outbox pending count and oldest age, consumer failures. Export is OTLP when `Observability:OtlpEndpoint` is set, nothing otherwise. Every log line carries `TraceId` and `SpanId`; one log line per request, never bodies.

## What varies

Where telemetry is sent, and the outbox lag threshold.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Logs and correlation ids only | Nothing | No timing, and no view across processes without searching logs |
| A vendor SDK | Lock-in | OpenTelemetry exports to any vendor |
| Trace context as a field of every event | A technical field in the domain's events | The outbox row and the envelope headers carry it outside the domain |
| **OpenTelemetry, trace context on the outbox row and in the envelope, readiness on outbox lag** | Packages in the hosts, a column, a collector when exporting | n/a |

## Consequences

- Gained: one trace per request across processes; a stuck outbox makes the service unready instead of silently piling up events; logs and traces join on the trace id.
- Lost or owed: a collector to run once exporting; MassTransit publishers start a new trace.
- Cost of going back: low.

## How it is enforced

`CommandPipelineTests`, `OutboxMessageTests`, `MessageEnvelopeTests`, and `observability.sh` (live and ready answers; ready fails without the database and on outbox lag; the API's and the consumer's log lines share a trace id).
~~~~

## 0023-testing-levels.md

~~~~markdown
# 0023 — How much testing structure the project starts with

- Status: accepted
- Date: <the date the project was generated>
- Context: The core ships unit tests with fakes for every class. Some failures only show when the pieces are wired together, when a real database answers, or when the code drifts from its own rules over months.

## Decision

The project starts at testing level `{testing}`. `handlers` adds a validator test per command and scenario tests through the real container, dispatcher and pipeline, built with `ValidateOnBuild`. `integration` adds `{Name}.IntegrationTests`: repositories, the unit of work, the outbox and the broker against real PostgreSQL and RabbitMQ in containers (or `MORTIS_TEST_POSTGRES` / `MORTIS_TEST_RABBITMQ`), one database per test class; without either the tests are skipped, never passed. `architecture` adds reflection rules (layer references, every command and sync handler registered, no private method in a `*Service`), a golden file pinning every queue event's wire name and JSON shape, and an 80% line-coverage gate. Each level includes the ones before it.

## What varies

The level, and the coverage threshold.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Unit tests only, whatever the project | Nothing | Wiring, SQL and contract drift are found in production |
| Every level for every project | Containers and a coverage gate in every CI | Too heavy for a prototype |
| **A level chosen per project, each including the ones before** | The chosen level's tests to keep | n/a |

## Consequences

- Gained: a missing registration fails a test, not the first request; the real SQL is exercised; a changed event contract is a reviewed change to one file.
- Lost or owed: Docker or a test database in CI from `integration`; the golden file is updated deliberately (`MORTIS_UPDATE_CONTRACTS=1`) when a contract changes on purpose.
- Cost of going back: low.

## How it is enforced

The level's own tests, and `testing.sh` (no integration project at `unit`; skipped, not passed, without a database; each architecture rule fails on a deliberately broken copy).
~~~~

## 0024-soft-delete.md

~~~~markdown
# 0024 — Deleting marks a row; it never removes it

- Status: accepted
- Date: <the date the project was generated>
- Context: Deleted data must stay for audit and undo, yet disappear from every read and free its natural key.

## Decision

A soft-deletable aggregate implements `ISoftDeletable` and raises `Deleted` and `Restored`. A named EF Core query filter hides deleted rows; ignoring it by name keeps the tenant filter. A natural key no other table references becomes a unique index filtered on `is_deleted = false`, so a deleted key can be reused and a restore that collides answers 409 `duplicate`; a natural key another table references (the example's SKU) stays an alternate key, because a foreign key needs a real unique constraint, and stays taken.

## What varies

Which aggregates are soft-deletable.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Hard delete | Nothing | Audit and undo are lost |
| An archive table | A copy per aggregate | Two schemas to keep in step |
| **A flag, a named filter, filtered unique indexes** | Raw SQL must add the filter by hand | n/a |

## Consequences

- Gained: deletes are reversible and audited; a deleted key is free again.
- Lost or owed: tables keep growing until the project writes a retention job; raw SQL and bulk updates add `is_deleted = false` by hand.
- Cost of going back: medium (rows deleted while on stay as rows).

## How it is enforced

The domain and handler tests, `ProductConfigurationTests` (the named filters), and `soft-delete.sh` (a deleted product is invisible, its referenced SKU stays taken, a restore brings it back).
~~~~

## 0025-caching-reads.md

~~~~markdown
# 0025 — Read answers are cached and invalidated by events

- Status: accepted
- Date: <the date the project was generated>
- Context: Some reads are slow and repeated. A cache that is not invalidated by the writes serves stale data, and a cache feeding a write corrupts it.

## Decision

Read handlers cache their answers through `ICacheService` (HybridCache: memory in each process, Redis behind it when configured), keyed by plural, tenant, id and language and tagged by aggregate instance. Every event that changes a cached answer has a sync handler that removes the tag. Commands always read the database.

## What varies

What is cached and for how long (`Caching:Seconds`).

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| No cache | Slower reads | Chosen against for the reads that need it |
| Time-based expiry only | Nothing to invalidate | Answers are stale for the whole lifetime |
| Cache the aggregate | One cache for reads and writes | A stale aggregate would feed a write |
| **Cache answers, invalidate by events** | A handler per changing event | n/a |

## Consequences

- Gained: repeated reads skip the database; a caller who just wrote reads fresh data.
- Lost or owed: a short window (the lifetime) where a concurrent read can put an old answer back; without Redis each process invalidates only its own memory.
- Cost of going back: low.

## How it is enforced

`HybridCacheServiceTests`, the invalidation handler tests, and `caching.sh` (a read after a change is fresh).
~~~~

## 0026-scheduled-jobs.md

~~~~markdown
# 0026 — Jobs on a clock run once per tick, as a named service user

- Status: accepted
- Date: <the date the project was generated>
- Context: Some work happens on a clock, not on a request. With several worker replicas a naive timer runs it once per replica.

## Decision

A recurring job implements `IRecurringJob` (a name, a default cron, `RunAsync` that sends commands). `SchedulerService` claims each due tick with an insert into `scheduled_job_runs` keyed by job and tick; only the winner runs it. Each job runs as the service user its settings name; there is no system user.

## What varies

The jobs, their schedules and their users.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A timer in each process | Nothing | Runs once per replica |
| A distributed lock library | A lock service | A tick claimed in the database needs nothing new |
| An external scheduler (cron, Quartz cluster) | Another system | Jobs belong next to the commands they send |
| **A row per tick, claimed by insert** | Two tables | n/a |

## Consequences

- Gained: any number of workers; a job's runs and failures are visible in a table; jobs are audited as their service user.
- Lost or owed: a failed tick is not retried until the next one; a service user per job to provision.
- Cost of going back: low.

## How it is enforced

`SchedulerTicks` tests and `scheduler.sh` (with two workers a job fires once per tick).
~~~~

## 0027-file-storage-port.md

~~~~markdown
# 0027 — Files live behind a storage port; rows hold keys

- Status: accepted
- Date: <the date the project was generated>
- Context: Files are uploaded and served. Bytes in the database bloat it; a storage SDK in the handlers ties them to one vendor.

## Decision

`IFileStorage` (put, open, delete) with an S3-compatible adapter (S3, MinIO) and a local-disk adapter, chosen by `Storage:Provider`. Keys are built from ids the application owns. A handler writes the file first and then the row that holds its key. Size and content type are validated.

## What varies

The provider, the bucket, the limits.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Bytes in the database | Nothing extra | Large rows and backups |
| The vendor SDK in handlers | No port | Vendor lock-in; untestable handlers |
| **A port with two adapters** | One interface | n/a |

## Consequences

- Gained: handlers are testable with a fake; development needs no cloud; production can move between S3-compatible stores.
- Lost or owed: an orphan file when the row write fails; their cleanup is the project's own job.
- Cost of going back: low.

## How it is enforced

`LocalDiskFileStorageTests`, `S3FileStorageTests` against MinIO, the handler tests, and `storage.sh` (an upload reads back byte for byte).
~~~~

## 0028-secrets-at-rest.md

~~~~markdown
# 0028 — Secret values are encrypted at rest with envelope keys

- Status: accepted
- Date: <the date the project was generated>
- Context: Some values (tax numbers, third-party keys) must be unreadable to anyone with the database or a backup.

## Decision

A secret property is mapped with `IsSecret`: an EF value converter around `ISecretProtector`. Each value gets a fresh AES-256-GCM data key, wrapped by the current key-encryption key from `Secrets:Keys`; the envelope names the key that wraps it, so rotation only adds a key. The domain sees plaintext.

## What varies

The keys, their rotation, which members are secret.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Database encryption at rest | Nothing in code | Anyone with a connection or a dump reads plaintext |
| One key encrypting every value | Simpler | Rotation means re-encrypting everything at once |
| **Envelope keys with the key id inside** | A converter and keys in a secret store | n/a |

## Consequences

- Gained: dumps and backups hold envelopes; keys rotate without downtime.
- Lost or owed: secret columns cannot be searched or indexed by value; losing a key loses its values.
- Cost of going back: high (values are only readable with the keys).

## How it is enforced

`EnvelopeSecretProtectorTests`, `CustomerConfigurationTests`, and `secrets.sh` (the column never holds the plaintext; the API reads it back).
~~~~

## 0029-loop-guard.md

~~~~markdown
# 0029 — A chain of reactions longer than 16 commands stops

- Status: accepted
- Date: <the date the project was generated>
- Context: Handlers that trigger each other, directly or through the outbox and the bus, would run forever and fill the logs, the queues and the database.

## Decision

The command context and every event carry the chain of command names that led to them. The pipeline appends the command's name and refuses to run a command whose chain is longer than `LoopGuardConstants.MaxChain` (16) with a 500 `loop_detected` that names the chain.

## What varies

The limit.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Nothing | Nothing | An accidental loop runs until someone notices |
| A depth number only | Smaller | The error cannot say which handlers loop |
| **The chain of names** | A list on the context and the event | n/a |

## Consequences

- Gained: a loop stops within a second and its log line names it.
- Lost or owed: every event carries a short list of names.
- Cost of going back: low.

## How it is enforced

`CommandPipelineTests`, `LoopGuardTests` (two handlers that trigger each other stop at the limit), and `loop-guard.sh`.
~~~~

## 0030-bounded-contexts.md

~~~~markdown
# 0030 — Bounded contexts are folders that talk through events and ids

- Status: accepted
- Date: <the date the project was generated>
- Context: Groups of aggregates have stopped sharing a vocabulary. Without a boundary, one group reaches into another's types and the two can no longer change apart.

## Decision

Code lives under `Contexts/{Context}` in every project. A context refers to another only through the other's events and ids; a reaction to another context's event is a handler or consumer in the reacting context. One process and one database.

## What varies

The contexts and which aggregates belong to each.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| One flat model | Nothing | Everything couples to everything |
| A service per context now | Deployments, network, data copies | The split is not proven yet |
| **Folders with a rule test** | A path segment and a test | n/a |

## Consequences

- Gained: contexts change apart and can become services later by moving folders.
- Lost or owed: a type two contexts both need is duplicated or becomes an event; wire names follow the new namespaces.
- Cost of going back: medium.

## How it is enforced

`ContextRulesTests`, the review skill's body-reference check, and `contexts.sh`.
~~~~

## 0031-event-contracts-project.md

~~~~markdown
# 0031 — Queue events live in a contracts project other services reference

- Status: accepted
- Date: <the date the project was generated>
- Context: Another service consumes this service's events and must not depend on its domain.

## Decision

The events that leave the process (`IQueueEvent`) live in `{Name}.Contracts`, which references only `{Name}.Shared` and is packable; the domain references it and raises them. Wire names come from the namespace or are pinned with `[MessageName]`.

## What varies

Which events are published.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Consumers reference the domain | Nothing | They couple to every internal type |
| Consumers copy the event types | No package | Copies drift silently |
| **A contracts project** | A package to version | n/a |

## Consequences

- Gained: consumers build against the contracts alone; the published surface is visible in one project.
- Lost or owed: renaming a contract member is a breaking change; the package must be versioned and published.
- Cost of going back: low.

## How it is enforced

`ContractsRulesTests`, `EventContractTests` at `architecture`, and `events-project.sh` (a consumer builds against the contracts only).
~~~~
