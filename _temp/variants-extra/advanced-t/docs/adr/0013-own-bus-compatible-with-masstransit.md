# 0013 — Events leave the service on our own RabbitMQ bus, wire-compatible with MassTransit 8

- Status: accepted
- Date: 2026-11-21
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
