# Publishing events directly, C#

Applies when `events.outbox` is `none`. Project `{Name}.Infrastructure`, folder `Messaging/`. Layer 1: `events.md`.

Two classes. After `COMMIT` the unit of work hands each queue event to `EventPublisher`, which logs it and runs its async handlers in a new scope, each in its own transaction, as the system user. Nothing is stored.

What can be lost: an event whose async handler had not run when the process died. Say so plainly when it matters; the outbox options remove it.

With an outbox, neither class exists in the project (`outbox.md`).

## EventPublisher

File `Messaging/EventPublisher.cs`, registered as a singleton by `AddInfrastructureModule`:

@@decl src/Boilerplate.Infrastructure/Messaging/EventPublisher.cs::EventPublisher@@

@@member src/Boilerplate.Infrastructure/Messaging/EventPublisher.cs::EventPublisher::PublishAsync@@

- One scope for the event; one transaction per handler, in `Sequence` order.
- A concurrency conflict is retried in a fresh transaction up to `MaxAttempts`; any other failure is logged and the next handler still runs.
- Its log lines carry the event's fields (`LogFields.Of(event)`, `logging.md`): `CorrelationId`, `CausationId`, `TenantId`, `UserId`, `Event`, `EventId`.
- An async handler that writes a contended row needs the `xmin` row version on that aggregate (`infrastructure-postgres.md`).

## MessagingConstants

File `Messaging/MessagingConstants.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs::MessagingConstants@@

@@member src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs::MessagingConstants::MaxAttempts@@

@@member src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs::MessagingConstants::RetryDelayMilliseconds@@

## The unit of work's part

`UnitOfWork` (`infrastructure-postgres.md`) takes `EventPublisher` and, after `COMMIT`, publishes each queue event; a failure is logged and never undoes the commit:

@@member src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::CommitTransactionAsync@@

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/EventPublisherTests.cs`, with `EventPublisherBuilder` in `TestSupport/Infrastructure/Messaging/`:

@@members test/Boilerplate.Tests/Infrastructure/Messaging/EventPublisherTests.cs::EventPublisherTests@@

@@members test/Boilerplate.Tests/TestSupport/Infrastructure/Messaging/EventPublisherBuilder.cs::EventPublisherBuilder@@

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings events-inprocess.md@@
