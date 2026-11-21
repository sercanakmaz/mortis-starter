# Event consumers, C#

Applies to every project. Project `{Name}.Infrastructure`, folder `Messaging/`; the lines that register consumers are in the host that runs them. Layer 1: `events.md`.

An event consumer is a class that reacts to a queue event after the command that raised it has committed: `{Action}When{Aggregate}{Event}EventConsumer : BaseEventConsumer<TEvent>` (`shared-domain.md`, `application.md`). Every consumer is a consumer of its own, in every publishing model: its own transaction, its own retries, its own failure.

## The one way to register a consumer

One line per consumer, in the `Program.cs` of the host that runs it, after `AddInfrastructureModule`:

```csharp
builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(3));
```

- Nothing is found by scanning: a consumer without its line never runs. `ApplicationModule` registers no consumer.
- Which host runs the consumers depends on the publishing model:

| `events.outbox` / `events.transport` | Hosts with the lines |
|---|---|
| `none` / `in-process` | `{Name}.Api` and `{Name}.Console` |
| `after-command` or `background` / `database` | every host that runs commands, `{Name}.Api` and `{Name}.Console` (they write one row per registered consumer), and `{Name}.Worker` when `hosts.worker` is `separate` (it runs the rows) |
| any outbox / `rabbitmq` | `{Name}.Worker` only |

- With `database`, a host writes outbox rows only for the consumers it registers, so every host in that row lists the same consumers.
- The endpoint's settings are code, never settings files: `Retry(n)` and, with `rabbitmq`, `Named(queue)`.
- `Retry(n)` means n retries after the first attempt, so `Retry(3)` is four attempts. It replaces every global retry setting:

| Model | What `Retry(n)` controls |
|---|---|
| `none` | `EventPublisher` runs the consumer up to n + 1 times, each in a fresh scope and transaction, then logs the failure |
| outbox with `database` | the row's `max_attempts` is n + 1; the row stays in the table when it reaches it |
| `rabbitmq` | the receive endpoint runs the consumer up to n + 1 times, then moves the message to `<queue>_error` |

## EventConsumerEndpoint

File `Messaging/EventConsumerEndpoint.cs`. One consumer's settings: the consumer, the event it takes (from its base class), the queue name and the retry count:

@@decl src/Boilerplate.Infrastructure/Messaging/EventConsumerEndpoint.cs::EventConsumerEndpoint@@

@@members src/Boilerplate.Infrastructure/Messaging/EventConsumerEndpoint.cs::EventConsumerEndpoint@@

- The queue is the consumer's name in kebab case without `EventConsumer`: `DecreaseStockWhenOrderLineAddedEventConsumer` gets `decrease-stock-when-order-line-added`. Only `rabbitmq` uses it; the other models ignore it.
- Retry defaults to 3 when the action does not set it.

## EventConsumerModule

File `Messaging/EventConsumerModule.cs`. Generate the version of the chosen transport.

With `in-process` or `database`, the consumers go into an `EventConsumerRegistry` that `EventPublisher` or the outbox reads; `AddInfrastructureModule` calls `AddEventConsumerRegistry` so the registry exists even in a host with no consumer:

@@members variant:lean/src/Boilerplate.Infrastructure/Messaging/EventConsumerModule.cs::EventConsumerModule@@

With `rabbitmq`, each line adds the consumer and its receive endpoint (`rabbitmq.md`); there is no registry:

@@members variant:advanced/src/Boilerplate.Infrastructure/Messaging/EventConsumerModule.cs::EventConsumerModule@@

## EventConsumerRegistry

File `Messaging/EventConsumerRegistry.cs`, only with `in-process` or `database`:

@@decl src/Boilerplate.Infrastructure/Messaging/EventConsumerRegistry.cs::EventConsumerRegistry@@

@@members src/Boilerplate.Infrastructure/Messaging/EventConsumerRegistry.cs::EventConsumerRegistry@@

- `For(eventType)` returns the event's consumers in the order of their lines; `EventPublisher` runs them in that order and the outbox adds their rows in that order.
- `ByConsumer(name)` finds the endpoint of an outbox row's `consumer` column.

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs`, with `RecordingEventConsumer` and `OtherRecordingEventConsumer` in `TestSupport/Shared/Domain/` (`testing.md`). The first test differs by transport:

@@members variant:lean/test/Boilerplate.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs::EventConsumerModuleTests@@

With `rabbitmq` the first test counts the receive endpoints instead of reading a registry:

@@member variant:advanced/test/Boilerplate.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs::EventConsumerModuleTests::AddEventConsumer_Should_RegisterTheConsumerAndOneReceiveEndpointPerConsumer@@

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings event-consumers.md@@
