# Events in process, C#

Applies when `events.outbox` is `none` and `events.transport` is `in-process`. Project `{Name}.Infrastructure`, folder `Messaging/`. Layer 1: `events.md`.

Committed queue events go to every registered `IEventDispatcher`. Two are registered, in this order: `LoggingEventDispatcher` writes the wire name to the log; `AsyncEventHandlerDispatcher` runs the async handlers in a new scope, each in its own transaction, as the system user.

What can be lost: an event whose async handler had not run when the process died. Say so plainly when it matters; the outbox options remove it.

## MessagingConstants

File `Messaging/MessagingConstants.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs::MessagingConstants@@

@@member src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs::MessagingConstants::MaxAttempts@@

@@member src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs::MessagingConstants::RetryDelayMilliseconds@@

## LoggingEventDispatcher

File `Messaging/LoggingEventDispatcher.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/LoggingEventDispatcher.cs::LoggingEventDispatcher@@

@@member src/Boilerplate.Infrastructure/Messaging/LoggingEventDispatcher.cs::LoggingEventDispatcher::Publish@@

- The wire name is `<namespace>:<type>`, for example `{Name}.Domain.Aggregates.Orders.Events:Completed`.

## AsyncEventHandlerDispatcher

File `Messaging/AsyncEventHandlerDispatcher.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/AsyncEventHandlerDispatcher.cs::AsyncEventHandlerDispatcher@@

@@member src/Boilerplate.Infrastructure/Messaging/AsyncEventHandlerDispatcher.cs::AsyncEventHandlerDispatcher::Publish@@

- One scope for the event; one transaction per handler.
- A concurrency conflict is retried in a fresh transaction up to `MaxAttempts`; any other failure is logged and the next handler still runs.
- An async handler that writes a contended row needs the `xmin` row version on that aggregate (`infrastructure-postgres.md`).

## Tests

`AsyncEventHandlerDispatcherTests` in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings events-inprocess.md@@
