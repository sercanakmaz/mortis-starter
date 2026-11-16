# Messaging: our own bus, wire-compatible with MassTransit

Decided on 2026-11-16. Built in [phase 1, step 09](../../plans/phase-1-skills/09-rabbitmq-bus.md).

## Decision

The starter ships its own small message bus, the same way it ships its own command dispatcher in place of a mediator library. It does three things: publish an event, consume an event, and move a failed message to an error queue. On the wire it is **indistinguishable from MassTransit 8 on RabbitMQ**: a service that consumes with MassTransit receives our messages, and our consumers receive messages a MassTransit service publishes.

### Why not MassTransit, and why not OpenTransit

| Option | Verdict |
|---|---|
| MassTransit 9 | A commercial product that needs a licence. A starter that others install cannot assume one. |
| MassTransit 8 | Free, but official maintenance ends after 2026. Fine for an existing system; wrong as the foundation of new ones. |
| OpenTransit (community fork of version 8) | Not published yet: its maintainers say they will start releasing after version 8 reaches end of support and recommend MassTransit 8 until then. It is a fork of a very large codebase with a small team. It is .NET only, so it does nothing for Go. Revisit once it has releases. |
| **Our own bus** | We use a fraction of MassTransit (publish, consume, retry, error queue). Go needs the same wire format written by hand anyway, so the format has to be specified here either way. |

## Scope

In: publish, consume, per-endpoint retry, error queue, skipped queue, the envelope, topology declaration, headers for correlation, tenant and user.

Out, on purpose: request/response, sagas and state machines, message scheduling, the routing slip, the mediator, transports other than RabbitMQ, serializers other than JSON with the envelope.

## Source of truth

1. The MassTransit 8 source (Apache 2.0) and its "Interoperability" and "RabbitMQ transport" documentation pages.
2. **The interoperability tests.** The test project references MassTransit 8 as a test-only dependency and proves both directions against a real RabbitMQ: our publisher to a MassTransit consumer, and a MassTransit publisher to our consumer. Every statement below that is marked *(verify)* is from documentation and memory and must be confirmed by those tests before it is relied on.

## Names

| Thing | Rule | Example |
|---|---|---|
| Message URN | `urn:message:<Namespace>:<TypeName>` | `urn:message:Shop.Domain.Aggregates.Orders.Events:Completed` |
| Message exchange | `<Namespace>:<TypeName>`, durable | `Shop.Domain.Aggregates.Orders.Events:Completed` |
| Nested type | `<Namespace>:<Outer>+<Inner>` *(verify)* | |
| Receive endpoint | The queue name the consumer host chooses, for example `shop-stock` | |
| Error queue | `<queue>_error` | `shop-stock_error` |
| Skipped queue | `<queue>_skipped` | `shop-stock_skipped` |
| Address | `rabbitmq://<host>/<vhost>/<name>`; the default vhost `/` is omitted *(verify)* | `rabbitmq://localhost/shop-stock` |

This is why event records keep bare names such as `Completed` inside a per-aggregate namespace: the exchange reads `...Orders.Events:Completed`.

A message type may override its exchange type (existing systems set `topic` or `fanout` per event) and its entity name. The default exchange type is `fanout`.

In Go there are no namespaces. Each event declares its wire name explicitly (`func (Completed) MessageName() string { return "Shop.Domain.Aggregates.Orders.Events:Completed" }`), so a Go service and a C# service agree by contract and not by accident.

## Topology

Publishing `T`:
- declare the exchange for `T` (durable, `fanout` unless overridden);
- publish to it with an empty routing key (or the configured key for a `topic` exchange), persistent delivery.

A receive endpoint named `Q` that consumes `T`:
- declare queue `Q` (durable);
- declare exchange `Q` (durable, `fanout`) and bind exchange `Q` to queue `Q`;
- declare the exchange for `T` and bind it to exchange `Q` (an exchange-to-exchange binding).

Failure, after retries are used up: publish the original body to exchange `Q_error` (bound to queue `Q_error`) with the fault headers below, then acknowledge. A message whose type no consumer on the endpoint handles goes to `Q_skipped`.

## Envelope

Content type `application/vnd.masstransit+json`. The body is a JSON object with camelCase names:

```json
{
  "messageId": "0199f1c2-...",
  "requestId": null,
  "correlationId": "…",
  "conversationId": "…",
  "initiatorId": "…",
  "sourceAddress": "rabbitmq://localhost/shop-api",
  "destinationAddress": "rabbitmq://localhost/Shop.Domain.Aggregates.Orders.Events:Completed",
  "responseAddress": null,
  "faultAddress": null,
  "messageType": ["urn:message:Shop.Domain.Aggregates.Orders.Events:Completed", "urn:message:Shop.Shared.Domain:EventBase"],
  "message": { "eventId": "…", "orderId": "…" },
  "expirationTime": null,
  "sentTime": "2026-11-16T15:04:05.123Z",
  "headers": {},
  "host": { "machineName": "…", "processName": "…", "processId": 1, "assembly": "…", "assemblyVersion": "…", "frameworkVersion": "…", "massTransitVersion": "…", "operatingSystemVersion": "…" }
}
```

- `messageType` lists the URN of the message type and of every base type and interface it implements. A consumer accepts a message when one URN matches the type it consumes.
- A reader must accept an envelope that has only `message` and `messageType`, ignore unknown members, and match member names without regard to case (existing services may serialize with Newtonsoft).
- Enum and date formats inside `message` *(verify against both the System.Text.Json and the Newtonsoft serializer of MassTransit 8)*.

Mapping from our event:

| Envelope | From |
|---|---|
| `messageId` | `EventBase.EventId` (the outbox row id, so the inbox can deduplicate) |
| `correlationId`, `conversationId` | the event's correlation id |
| `initiatorId` | the event's causation id |
| `headers["Mortis-Tenant-Id"]`, `headers["Mortis-User-Id"]`, `headers["Mortis-User-Email"]`, `headers["Mortis-Language"]` | the event's context |
| `sentTime` | the publish time, not `OccurredAt` |

A message that arrives without the `Mortis-*` headers (published by a MassTransit service) is handled as the system user with no tenant.

## Transport properties

*(verify each)* `content_type` = `application/vnd.masstransit+json`, `delivery_mode` = 2, `message_id` = the envelope's `messageId`, `correlation_id` when present. A faulted message moved to `_error` carries `MT-Reason` = `fault`, `MT-Fault-ExceptionType`, `MT-Fault-Message`, `MT-Fault-StackTrace`, `MT-Fault-Timestamp`, `MT-Fault-ConsumerType` and `MT-Host-*` headers. A skipped message carries `MT-Reason` = `dead-letter`.

## Programming model

Shaped like MassTransit's so that moving a consumer is a change of `using` lines:

```
interface IConsumer<TMessage>        { Consume(context: ConsumeContext<TMessage>) }
interface ConsumeContext<TMessage>   { Message, MessageId, CorrelationId, ConversationId, InitiatorId, Headers, SentTime, CancellationToken }
interface IEventBus                  { Publish(event), PublishRange(events) }
```

Inside a scaffolded solution an application event handler does not implement `IConsumer` itself. The consumer host has one generic consumer per event type that restores the context (tenant, user, correlation), checks the inbox, and runs the `BaseAsyncEventHandler<T>` implementations, so a handler is written once and works with every transport. `IConsumer<T>` is for messages that are not our own events.

Registration is explicit, one line per endpoint and consumer, as everywhere else.

## What "compatible" is tested to mean

1. A MassTransit 8 consumer (System.Text.Json) receives an event we publish, with the right message, `MessageId` and `CorrelationId`.
2. The same with MassTransit's Newtonsoft serializer.
3. Our consumer receives an event published by MassTransit 8, with either serializer.
4. We bind to an exchange MassTransit declared, and MassTransit binds to one we declared, without a "precondition failed" error (same durability, type and arguments).
5. A failing consumer leaves the message in `<queue>_error` with the fault headers.
6. The Go bus passes the same five checks against the C# services.
