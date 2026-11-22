# Add an event consumer to a host

The line that makes a host run a consumer, with its retry count.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | [hosts.worker=none\|in-api] `src/{Name}.Api/Program.cs`: `services.AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(n))` | `event-consumers.md` | AGENTS 13 | the transport's conformance |
| 2 | [hosts.worker=separate] `src/{Name}.Worker/Program.cs`: the same line | `event-consumers.md`, `outbox.md` | AGENTS 13 | the transport's conformance |
| 3 | [events.transport=rabbitmq] `.Named("queue-name")` only to keep an existing queue; otherwise the queue is named from the consumer | `rabbitmq.md` | none | `transport-rabbitmq.sh` |
| 4 | the Console registers the consumers it must run for its own commands (`event-consumers.md`) | `hosts.md` | AGENTS 13 | `example-console.sh` |
