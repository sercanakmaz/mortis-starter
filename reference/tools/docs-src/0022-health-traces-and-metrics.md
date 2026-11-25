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
