# The outbox, C#

Applies when `events.outbox` is `background` (publish in the command) or `after-command` (publish after the command). Project `{Name}.Infrastructure`, folder `Messaging/Outbox/`; the Worker project when `hosts.worker` is `separate`. Layer 1: `events.md` "The three publishing models", `options/events.md`.

Generate only the chosen mode. The files below are the `background` code; a box marked **after-command** lists the only additions of that mode. With `none` nothing on this page exists (`events-inprocess.md`).

## How it fits together

```
command -> unitOfWork.CommitTransactionAsync
  save -> sync handlers -> collect the queue events
  outboxRepository.Add(events)          one row per event and registered consumer, in the SAME transaction
  save, COMMIT
  after-command only: outboxWorker.PublishAsync(id) for each of its rows

OutboxWorker (hosted, every PollingIntervalMilliseconds)
  repeat until nothing is due:
    new scope, BEGIN
    claim the next due row ("for update skip locked")
    run the row's consumer in this transaction
    delete the row, COMMIT
    on failure: ROLLBACK, count the attempt, wait 2^attempts seconds
```

- Every event consumer is a consumer of its own: it gets its own row, transaction, retries and failure. A failing consumer never blocks or repeats another consumer of the same event.
- One row, its consumer and its deletion are one transaction: a row is either fully handled and gone, or untouched and retried. No inbox is needed.
- An event without a registered consumer gets no row. Consumers are registered in the hosts (`event-consumers.md`).
- `skip locked` lets two workers, or a request and a worker, run side by side; each row goes to one of them.
- Each row carries `max_attempts`, its consumer's `Retry(n)` + 1. A row that reaches it stays in the table for an operator. To retry it: `update outbox_messages set attempts = 0, next_attempt_at = now() where id = '<id>'`.
- Rows are taken in `occurred_at, id` order; a retried row can come after a later one.

## The files

| File | Job |
|---|---|
| `OutboxMessage.cs` | The row: the event as JSON, the consumer that takes it, its attempts and attempt limit, when to try next, the last error |
| `OutboxMessageConfiguration.cs` | Its table, `outbox_messages` |
| `OutboxRepository.cs` | Add a row per event and registered consumer, claim the next due row, delete it, record a failure |
| `OutboxWorker.cs` | The background loop that publishes, sweeps and retries |
| `OutboxSettings.cs` | The polling interval (and, with `rabbitmq`, the longest wait between publish attempts) |
| `OutboxModule.cs` | The registrations |

## OutboxMessage

File `Messaging/Outbox/OutboxMessage.cs`:

@@decl variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessage.cs::OutboxMessage@@

@@members variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessage.cs::OutboxMessage@@

- `EventId` is the event's id, `Consumer` the full name of the consumer that takes this row, `MaxAttempts` its `Retry(n)` + 1. The correlation, causation, tenant and user travel inside the payload, as they do on the event.
- With `events.transport` = `rabbitmq` the row has no `EventId`, `Consumer` or `MaxAttempts`: there is one row per event, its id is the event id, the broker fans it out to one queue per consumer, and publishing is retried until the broker takes it (`rabbitmq.md`).
- `Type` is the event's full type name, read back against the Domain assembly. Renaming or moving an event type strands its pending rows: publish them first.

File `Messaging/Outbox/OutboxMessageConfiguration.cs` (picked up by `ApplyConfigurationsFromAssembly`):

@@decl variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs::OutboxMessageConfiguration@@

@@member variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs::OutboxMessageConfiguration::Configure@@

## OutboxRepository

File `Messaging/Outbox/OutboxRepository.cs`, scoped, on the request's `AppDbContext`:

@@decl variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxRepository.cs::OutboxRepository@@

@@members variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxRepository.cs::OutboxRepository@@

- `Add` only tracks the rows; the unit of work saves them before `COMMIT`.
- The raw SQL uses the snake_case names of `naming.database`.

**after-command** adds one member, to claim the event's next fresh row (a row that already failed is left to the worker's backoff):

@@member variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxRepository.cs::OutboxRepository::ClaimAsync@@

## OutboxWorker

File `Messaging/Outbox/OutboxWorker.cs`. This is the `database` transport, which runs the consumers itself; with `events.transport` = `rabbitmq` the worker publishes the row instead (`rabbitmq.md`).

@@decl variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxWorker.cs::OutboxWorker@@

@@member variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxWorker.cs::OutboxWorker::ExecuteAsync@@

@@member variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxWorker.cs::OutboxWorker::PublishAsync#2@@

- Its log lines carry the event's fields (`logging.md`): `CorrelationId`, `CausationId`, `TenantId`, `UserId`, `Event`, `EventId`.
- The consumers' commands join the worker's transaction; events they raise become rows in it.

**after-command** adds one member, which the unit of work calls right after `COMMIT`; it handles the event's rows one consumer at a time:

@@member variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxWorker.cs::OutboxWorker::PublishAsync@@

## OutboxSettings

File `Messaging/Outbox/OutboxSettings.cs`, bound to the `Outbox` section; the defaults need no `appsettings.json` entry:

@@decl src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxSettings.cs::OutboxSettings@@

@@members src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxSettings.cs::OutboxSettings@@

## OutboxModule

File `Messaging/Outbox/OutboxModule.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxModule.cs::OutboxModule@@

@@members src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxModule.cs::OutboxModule@@

**after-command** registers the worker once, so the unit of work and the host share it:

@@members variant:standard/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxModule.cs::OutboxModule@@

## The kernel files that change

`InfrastructureModule` registers the outbox instead of `EventPublisher`:

@@member variant:standard/src/Boilerplate.Infrastructure/InfrastructureModule.cs::InfrastructureModule::AddInfrastructureModule@@

`UnitOfWork` takes `OutboxRepository` instead of `EventPublisher` and `ILogger`, and writes the rows before `COMMIT`. **background**:

@@member variant:advanced/src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::CommitTransactionAsync@@

**after-command** also takes `OutboxWorker` and publishes its own rows after `COMMIT`:

@@member variant:standard/src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::CommitTransactionAsync@@

## Hosts

The worker runs in exactly one kind of host: the Api when `hosts.worker` is `in-api`, the Worker when it is `separate`. Api `Program.cs`, after `.AddInfrastructureModule(builder.Configuration)`, only for `in-api`:

```csharp
    .AddOutboxWorker()
```

The Console never runs the worker. With `background` its commands only write rows; with `after-command` each command publishes its own rows before the Console exits.

### The Worker

Only when `hosts.worker` is `separate`. Create it with `dotnet new worker -n {Name}.Worker -o src/{Name}.Worker`, add it to the solution, remove the template's `Worker.cs`.

File `src/{Name}.Worker/{Name}.Worker.csproj`:

@@data src/Boilerplate.Worker/Boilerplate.Worker.csproj xml@@

File `src/{Name}.Worker/Program.cs`. `.AddOutboxWorker()` is there only with an outbox, and the `.AddEventConsumer<...>(...)` lines with `database` or `rabbitmq` (`event-consumers.md`):

@@data src/Boilerplate.Worker/Program.cs csharp@@

File `src/{Name}.Worker/appsettings.json`; the `RabbitMq` connection string only with `events.transport` = `rabbitmq`:

@@data src/Boilerplate.Worker/appsettings.json json@@

## Migration

The table is part of the project's first migration (`infrastructure-postgres.md` "Migrations"), with or without the example.

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs`. With `events.transport` = `rabbitmq` (one row per event, `Id` is the event's id, tenant kept):

@@members variant:advanced/test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs::OutboxMessageTests@@

With `database` (one row per event and consumer, a fresh `Id`, the consumer's attempt limit; using `{Name}.Infrastructure.Messaging` as well):

@@members variant:standard/test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs::OutboxMessageTests@@

The claim, the worker and the retry need a real database; the conformance suite proves them (`scripts/conformance/outbox.sh`).

## Decision record

`docs/adr/0011-outbox-and-publishing-models.md`, in `project-docs-adr.md`.

## Namespaces and usings

The `UnitOfWork` variants use `{Name}.Infrastructure.Messaging.Outbox` instead of `{Name}.Infrastructure.Messaging` and drop `Microsoft.Extensions.Logging`.

@@usings outbox.md@@
