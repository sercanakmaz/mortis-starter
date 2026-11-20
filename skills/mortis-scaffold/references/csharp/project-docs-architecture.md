# Project documents: docs/architecture.md, C#

Written exactly as shown, replacing `{Name}`. With `example` = `false` the examples in it still name the order domain; they illustrate and stay.

~~~~markdown
# Architecture

How a request moves through this solution, and why each piece is where it is. The rules that follow from it are in [AGENTS.md](../AGENTS.md).

## Contents

1. Projects
2. What happens when a command runs
3. Domain events
4. Persistence
5. The Console project
6. What is deliberately not here
7. When to split something out
8. Known gaps

## 1. Projects

| Project | Holds | May reference |
|---|---|---|
| `Shared` | Building blocks with no database library: `Entity`, `AggregateRoot`, `EventBase`, `EventContext`, `BaseSyncEventHandler`, `BaseAsyncEventHandler`, `ICommand<TContent>`, `IContextCommand`, `CommandContext`, `ITransactionalCommand`, `BaseCommandHandler`, `ICommandDispatcher`, `CommandPipeline`, `IRepository`, `IUnitOfWork`, exceptions | nothing of ours |
| `Domain` | `Aggregates/<Plural>/`: aggregate root, child entities, value objects, `Events/`, repository **interface**, constants; `Services/` and `Ports/` when needed | `Shared` |
| `Application` | `<Plural>/`: commands and DTOs | `Domain`, `Shared` |
| `Infrastructure` | Everything that implements a Domain or Shared interface: `AppDbContext`, `EfRepository`, `UnitOfWork`, repositories, EF configurations, migrations, logging, outbound event dispatcher | `Domain`, `Shared` |
| `Api` | HTTP host | `Application`, `Infrastructure` |
| `Console` | Manual-testing host | `Application`, `Infrastructure` |

`Domain` and `Application` never see EF Core. That is why a repository is split in two: the interface sits beside its aggregate, the class and the EF mapping sit in `Infrastructure/Persistence/<Plural>/`.

## 2. What happens when a command runs

A controller, the Console or an event handler calls `Send` on `ICommandDispatcher` with a command. It does not know, and cannot name, the handler: the command declares its result type (`ICommand<OrderDto>`), the dispatcher finds the `BaseCommandHandler<TCommand, TContent>` registered for that command type and calls it. `BaseCommandHandler` passes the call to `CommandPipeline<TCommand>`, which does this, in this order:

1. **Validate.** Every registered `IValidator<TCommand>` runs. Failures throw `ValidationFailedException` (HTTP 422) before anything else happens. Because validation is part of the pipeline, a command is validated no matter who calls it.
2. **Middlewares, before.** Each `ICommandHandlerMiddleware.Before` runs.
3. **Begin a transaction** if the command implements `ITransactionalCommand`.
4. **`OnHandle`.** The handler loads the aggregates the action needs through their repositories, calls their operations (or a domain service when the decision needs several entities or another system), stamps the user and saves.
5. **Commit.** `UnitOfWork.CommitTransactionAsync` saves, runs the domain event handlers (still inside the transaction), commits, then publishes queue events and runs the `OnCommitted` actions.
6. **Middlewares, after.**

Any exception between 3 and 5 rolls the transaction back, clears the events, and is rethrown. The Api's `ExceptionHandler` turns an `ExceptionBase` into `{ code, message, errors }` with the exception's HTTP status.

A command that calls another command joins the outer transaction: the first one to begin owns the commit.

A command without `ITransactionalCommand` gets no transaction. If it tries to write, the repository throws `TransactionRequiredException`. This is deliberate: a write that silently runs without a transaction also silently loses its events.

### Who did it, and as part of what

There is no ambient "current user" or "current request". What the host knows about the call is data on the command, in one property:

- A command that writes implements `IContextCommand`: one line, `[JsonIgnore] public CommandContext Context { get; set; }`. The context holds the user, the correlation id of the whole request, the causation id (the event that led to this command, if any), and the tenant and language when those options are on. The client can neither send nor read it.
- **The host fills it.** The Api's `ContextCommandFilter` builds one context per request from the claims and the request's correlation id. The Console's `.AsConsoleUser()` sets the console user and one correlation id per run. A command that arrives without a context gets one with a new correlation id from the pipeline.
- The handler passes `command.Context.User` to the aggregate (`new Order(..., user)`, `order.SetUpdatedAudit(user)`). Queries have no context: they write nothing.
- **An event carries the context of the command that raised it.** `AggregateRoot` stamps the user; while a command runs, `EventContext` stamps its correlation id, causation id, tenant and language on every event its aggregates raise. An event handler gives the command it sends `ContextOf(@event)`: the same correlation id, the event as its cause, and the event's user (sync) or `SystemUser` (async). One correlation id therefore follows a request through every command and event it causes, and each causation id says what caused each step.

## 3. Domain events

```
aggregate method        RaiseEvent(new LineCancelled(...))      recorded on the aggregate
repository write        events move to EventContext, aggregate is cleared, SQL is sent
commit, before COMMIT   EventHandlerExecutor runs every BaseSyncEventHandler<TEvent>, awaited, by Sequence
                        a handler may write and may raise more events; those are handled too
COMMIT
commit, after COMMIT    each event marked IQueueEvent goes to EventPublisher, which runs every
                        BaseAsyncEventHandler<TEvent> in a new scope, each in its own transaction, as `SystemUser`
                        then the OnCommitted actions run
```

| | Sync handler | Async handler |
|---|---|---|
| Base class | `BaseSyncEventHandler<TEvent>` | `BaseAsyncEventHandler<TEvent>` (event must be `IQueueEvent`) |
| Runs | before COMMIT, in the transaction of the command | after COMMIT, in a new scope and its own transaction |
| If it throws | the whole command rolls back and the caller gets the error | logged, the original change stays committed |
| Can veto | yes | no |
| Consistency | strong: all or nothing | eventual: the next read may not show it yet |
| Typical use | a rule that must hold together with the change: cancel the order when its last line is cancelled, count a completed order on the customer | stock, read models, notifications |

- **Inside the transaction** means all-or-nothing. If a sync handler throws, the command that raised the event is rolled back with it.
- **Where they live.** An event handler is in the Application layer, next to the commands, and sends a command. The command runs through the same pipeline as a request (validation, logging) and joins the transaction that is already open: inside a sync handler that is the command's own transaction, in an async handler it is the one the dispatcher began. If the inner command throws, the transaction is rolled back and the error reaches whoever began it.
- **After the commit** is for anything that may lag behind or fail on its own. An async handler that hits a `DbUpdateConcurrencyException` is retried in a fresh transaction up to `MessagingConstants.MaxAttempts` times; any other failure is logged and the next handler still runs. Register other after-commit work with `IUnitOfWork.OnCommitted`.
- **A command result is built before COMMIT**, so it does not show what a sync handler changed afterwards. Read the aggregate again to see it.
- An async handler that writes to a contended row needs a concurrency token on that aggregate (`Product` uses PostgreSQL's `xmin`); without one, two handlers read the same stock and one update is lost.
- An event is raised **before** the repository write, inside the aggregate. The repository drains events at the start of `CreateAsync`, `UpdateAsync` and `DeleteAsync`; an event raised after that call would stay on the aggregate and never be handled. `RaiseEvent` is `protected` so that a service cannot do this by accident.
- A reaction in another aggregate is a handler in **that** aggregate's folder which sends that aggregate's own command. A command handler changes a second aggregate itself only when the action is about both (a transfer); a consequence is always an event handler.
- The exchange name of a queue event is `Namespace:Type`, for example `{Name}.Domain.Aggregates.Orders.Events:Completed`. This is why event records keep bare names in their aggregate's own `Events` namespace.

## 4. Persistence

- **Writes flush immediately.** Each repository write ends with `SaveChangesAsync`, so a later query in the same command sees it, and a database error surfaces at the line that caused it.
- **Tracking.** `FindByIdAsync` is a no-tracking read. `FindByIdTrackingAsync` is the read you change and save. `UpdateAsync` refuses an aggregate that is not tracked.
- **Ids never come from the database.** `AggregateConfiguration` and `ChildEntityConfiguration` set `ValueGeneratedNever()`. With database-generated keys, EF treats a new child that already has an id as an existing row and sends an `UPDATE` that matches nothing.
- **Child entities** are mapped with `HasMany(...).WithOne()` plus `Navigation(...).AutoInclude()`, so an aggregate always loads whole. They have no `DbSet` of their own.
- **Value objects** are mapped with `ComplexProperty(...)` and `IsRequired()`.
- **Uniqueness** is a unique index. The database refuses the duplicate and `DatabaseExceptionHandler` turns it into `DuplicateException` (409); handlers never check first.
- **References to other aggregates** are foreign keys with an explicit delete behaviour. PostgreSQL errors are translated by `DatabaseExceptionHandler`: unique violation → `DuplicateException` (409), foreign key violation → `ReferenceViolationException` (400).
- **One scope, one unit of work.** `UnitOfWork` belongs to the dependency-injection scope. Background work that processes several items opens a scope per item.
- **No views or functions in the database.** A read that joins tables is SQL in a `*Constants` class run by the repository, so schema changes never have to know about hidden database objects.

## 5. The Console project

`{Name}.Console` is a host for testing by hand: the same wiring as the Api, and it runs one command per run.

```bash
dotnet run --project src/{Name}.Console -- CreateProduct '{"sku":"BOLT","name":"Bolt","stock":10}'
dotnet run --project src/{Name}.Console -- CreateProduct @product.json            # JSON from a file
dotnet run --project src/{Name}.Console -- GetProduct '{"id":"<id>"}'
```

- The first argument is the command's type name without `Command` (case ignored); the runner finds it in the Application assembly, so a new command needs nothing in the Console. The second is the JSON (names ignore case; empty means the command's defaults).
- stdout holds only the result as JSON; the log lines go to stderr. A failure is one line `{ code, message, errors }` on stderr and exit code 2; no command name is exit code 1.
- A flow that spans several commands is several runs. The Console user is `console`; each run has its own correlation id.
- **VS Code:** `.vscode/launch.json` asks for the name and the JSON each time you press F5. It needs the C# extension and builds the Console first through the `build-console` task.

## 6. What is deliberately not here

| Not used | Used instead | Why |
|---|---|---|
| A mediator library | `ICommandDispatcher` + `BaseCommandHandler` + `CommandPipeline` | No dependency, no license terms, and validation runs on every entry point |
| A mapping library | `static XDto Map(X x)` | A mapper hides which fields a response reads |
| An assertion library | xUnit `Assert` | One style, no extra dependency |
| Assembly-scan registration | Explicit lines in the module files | A missing registration is visible in a diff |
| A generic repository beyond `EfRepository` | A repository interface per aggregate | Queries are named after what the domain asks |
| A service per aggregate | Command handlers that load and save; domain services only for logic that fits no aggregate | A per-aggregate service collects every operation and hides where the use case is |
| Database-generated ids, views, functions | See §4 | |

The decisions behind this layout are written down in [adr/](adr/): [0001](adr/0001-commands-are-sent-through-a-dispatcher.md) the dispatcher, [0002](adr/0002-commands-are-records-that-implement-interfaces.md) commands as records with interfaces, [0003](adr/0003-the-user-is-data-on-the-command.md) the user on the command, [0004](adr/0004-event-handlers-live-in-application-and-send-commands.md) event handlers, [0005](adr/0005-the-console-runs-commands-and-does-not-migrate.md) the Console, [0006](adr/0006-command-handlers-are-the-use-cases.md) command handlers as use cases, [0007](adr/0007-one-context-on-the-command.md) one context on the command, [0012](adr/0012-json-log-lines.md) the JSON log lines.

## 7. When to split something out

- **A separate `Events` project** when another process (a consumer service) needs the event contracts without the domain.
- **Bounded contexts** when two groups of aggregates stop sharing a vocabulary. Move them to `Aggregates/<Context>/<Plural>/` in Domain and mirror that in Application and Infrastructure. Contexts talk through events and ids only.
- **A second database or schema** needs a second `DbContext` and a second unit of work. Treat it as an architecture decision and write it down in `docs/adr/`.

## 8. Known gaps

- Queue events are published in process after the commit. There is no outbox, so a crash between the commit and an async handler loses that handler's work.
- There is no guard against two event handlers that trigger each other forever.
- Error messages are English only.
- Authentication is not configured. `ContextCommandFilter` reads the standard claims once it is; until then the audit user of an Api write is empty.
- `EfRepository` and the EF mappings are tested without a database (change-tracker state and model shape). Behaviour that only a real PostgreSQL shows is checked by running Console commands against it.
~~~~
