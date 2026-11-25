# The RabbitMQ bus, C#

Applies when `events.transport` is `rabbitmq` (it needs an outbox and, by rule R3, a worker host). Project `{Name}.Infrastructure`, folder `Messaging/RabbitMq/`; the consumers in the Worker. Layer 1: `events.md` "Worker", `options/events.md`. The wire format is MassTransit 8's, checked against MassTransit 8.5 with both its serializers.

Our own small bus on `RabbitMQ.Client`: it publishes outbox rows and consumes them in the Worker. Every event consumer is a consumer of its own, as in MassTransit: its own queue, transaction, retries and error queue. A MassTransit 8 service consumes what we publish, and we read what it publishes. No MassTransit package is referenced.

## How it fits together

```
OutboxWorker (Api or Worker)       claim a row, RabbitMqPublisher.PublishAsync, wait for the broker's confirm, delete the row, COMMIT
RabbitMqPublisher                  envelope (MassTransit 8 JSON) to the durable fanout exchange "<Namespace>:<TypeName>"
ReceiveEndpointService (Worker)    one per AddEventConsumer line: queue Q (the consumer's name), exchange Q, the event's exchange bound to Q, Q_error
  per message                      read the envelope; not the consumer's event: acknowledge; else new scope, BEGIN, the consumer, COMMIT
  on failure                       retry Retry(n) times, then publish the body to exchange Q_error with the MT-Fault headers, ack
```

- One consumer's failure never touches another consumer's queue: each retries and fails alone.
- A message whose types do not include the consumer's event is acknowledged and logged.
- The broker delivers at least once: a message can reach a consumer twice (a crash after its commit and before the acknowledgement, a confirm lost on the way back). **Every event consumer must be safe to run twice**; the bus does not deduplicate. A consumer that cannot be made so naturally keeps its own record of what it handled, in its own tables, in the same transaction.
- A broker that is down fails the publish, so the row stays and the outbox retries it with its backoff. A receive endpoint that cannot reach the broker tries again every `ReconnectSeconds`; the connection recovers by itself after that.
- An endpoint's configuration is code, one `AddEventConsumer<TConsumer>(endpoint => ...)` line per consumer in the Worker (`event-consumers.md`), never settings: `Retry(n)` and, when the default name does not fit, `Named(queue)`.

## The files

| File | Job |
|---|---|
| `RabbitMqConstants.cs` | Content type, address prefix, error suffix, queue type, prefetch, reconnect delay |
| `MessageNames.cs` | Exchange name and message URNs of a type |
| `MessageEnvelope.cs` | The MassTransit 8 envelope: build from an event, write, read, turn into an event |
| `RabbitMqConnection.cs` | One connection, a channel per use |
| `RabbitMqPublisher.cs` | Publish one event, with the broker's confirm |
| `ReceiveEndpointService.cs` | The consume loop of one consumer's endpoint; the consumer runs in one transaction |
| `RabbitMqModule.cs` | The connection and the publisher |

## Names

File `Messaging/RabbitMq/RabbitMqConstants.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqConstants.cs::RabbitMqConstants@@

@@members src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqConstants.cs::RabbitMqConstants@@

File `Messaging/RabbitMq/MessageNames.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/RabbitMq/MessageNames.cs::MessageNames@@

@@members src/Boilerplate.Infrastructure/Messaging/RabbitMq/MessageNames.cs::MessageNames@@

- `MessageTypes` lists the type, its base types and its interfaces outside `System`, as MassTransit does: `LineAdded` gives the URNs of `LineAdded`, `EventBase` and `IQueueEvent`.
- Renaming or moving an event type changes its exchange; a consumer elsewhere stops hearing it.

## The envelope

File `Messaging/RabbitMq/MessageEnvelope.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/RabbitMq/MessageEnvelope.cs::MessageEnvelope@@

@@members src/Boilerplate.Infrastructure/Messaging/RabbitMq/MessageEnvelope.cs::MessageEnvelope@@

- `messageId` is the event id, so a consumer that deduplicates can use it; `correlationId` and `conversationId` are the event's correlation; `initiatorId` is its causation.
- The context also travels as `Mortis-Tenant-Id`, `Mortis-User-Id`, `Mortis-User-Email` and `Mortis-Language` headers, for consumers that are not ours.
- `Read` matches names without regard to case and ignores unknown members, so a Newtonsoft envelope (PascalCase, nulls left out) reads too. A message without our context fields takes the envelope's correlation.

## Publishing

File `Messaging/RabbitMq/RabbitMqConnection.cs`, a singleton:

@@decl src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqConnection.cs::RabbitMqConnection@@

@@members src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqConnection.cs::RabbitMqConnection@@

File `Messaging/RabbitMq/RabbitMqPublisher.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqPublisher.cs::RabbitMqPublisher@@

@@members src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqPublisher.cs::RabbitMqPublisher@@

- The exchange is declared on every publish with the arguments MassTransit uses (durable fanout, no arguments), so neither side gets `PRECONDITION_FAILED`.
- With publisher confirms on, `BasicPublishAsync` returns only after the broker took the message, and throws when it refused it.

`OutboxWorker` takes `RabbitMqPublisher` and publishes the row instead of running a consumer; there is one row per event, with no `EventId`, `Consumer` or `MaxAttempts` column (`OutboxMessage.From(event)`, `OutboxRepository.Add` adds one row per event). Publishing is retried until the broker takes it, waiting 2^attempts seconds but never more than `OutboxSettings.MaxBackoffSeconds`; everything else is as on `outbox.md`:

@@member variant:advanced/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxWorker.cs::OutboxWorker::PublishAsync@@

## Consuming

The consumer's endpoint is an `EventConsumerEndpoint` (`event-consumers.md`); its `Queue` is the queue name. File `Messaging/RabbitMq/ReceiveEndpointService.cs`, one per `AddEventConsumer` line:

@@decl src/Boilerplate.Infrastructure/Messaging/RabbitMq/ReceiveEndpointService.cs::ReceiveEndpointService@@

@@members src/Boilerplate.Infrastructure/Messaging/RabbitMq/ReceiveEndpointService.cs::ReceiveEndpointService@@

- Queues are declared `classic` and durable, exactly as MassTransit 8 declares them; the same queue name can be declared by either side.
- The fault headers are MassTransit's: `MT-Reason` = `fault`, `MT-Fault-ExceptionType`, `MT-Fault-Message`, `MT-Fault-StackTrace`, `MT-Fault-Timestamp`, `MT-Fault-ConsumerType`, `MT-Fault-InputAddress`, `MT-Fault-MessageType`.
- The consumers restore the context from the event (`ContextOf(@event)`); their commands join the consumer's transaction, and the events they raise become outbox rows in it.

## Registration

File `Messaging/RabbitMq/RabbitMqModule.cs`:

@@decl src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqModule.cs::RabbitMqModule@@

@@members src/Boilerplate.Infrastructure/Messaging/RabbitMq/RabbitMqModule.cs::RabbitMqModule@@

`InfrastructureModule` adds one line after `services.AddOutboxModule(configuration);`:

```csharp
        services.AddRabbitMqModule();
```

The Worker's `Program.cs` adds one line per consumer after `.AddOutboxWorker()` (the whole file is on `outbox.md`, the `AddEventConsumer` of this transport on `event-consumers.md`):

```csharp
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10));
```

The Api and the Console register no consumer.

Every host's `appsettings.json` gets `"RabbitMq": "amqp://guest:guest@localhost:5672/"` under `ConnectionStrings`; `docker-compose.yml` gets a `rabbitmq:4-management` service with a health check, and the Api depends on it.

Packages: `RabbitMQ.Client` 7.2.2 in `Directory.Packages.props` and `{Name}.Infrastructure.csproj`.

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/RabbitMq/MessageNamesTests.cs`:

@@members test/Boilerplate.Tests/Infrastructure/Messaging/RabbitMq/MessageNamesTests.cs::MessageNamesTests@@

File `test/{Name}.Tests/Infrastructure/Messaging/RabbitMq/MessageEnvelopeTests.cs`. The two bodies were written by MassTransit 8.5.12, one per serializer:

@@members test/Boilerplate.Tests/Infrastructure/Messaging/RabbitMq/MessageEnvelopeTests.cs::MessageEnvelopeTests@@

The broker behaviour is proven by the conformance suite (`scripts/conformance/transport-rabbitmq.sh`).

## Decision record

`docs/adr/0013-own-bus-compatible-with-masstransit.md`, in `project-docs-adr.md`.

## Namespaces and usings

@@usings rabbitmq.md@@
