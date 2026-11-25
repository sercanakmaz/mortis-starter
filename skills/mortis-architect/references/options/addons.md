# Add-ons

Optional parts, each adding files and registration lines. Choose several; the value is a comma-separated list.

## addons

The add-ons switched on.

**By preset:** lean `none`, standard `correlation, api-conventions`, advanced `correlation, soft-delete, caching, scheduler, secrets, loop-guard, events-project, idempotent-commands, observability, api-conventions`.

### correlation

**Choose when:** a caller (a front end, another service) must pass its own correlation id in and find every log line of what it caused. Without the add-on every command and event still carries a correlation and a causation id, made per request.

**Costs:** one middleware: the `X-Correlation-Id` header read and returned, and a logging scope per request.

### soft-delete

**Choose when:** deleted data must stay for audit or undo.

**Costs:** a named filter on every query of a soft-deletable aggregate; natural keys no other table references become filtered indexes (a referenced one stays taken); raw SQL must add the filter by hand; a restore can collide with a reused key. Built and proved in the C# advanced reference solution (decision 52).

### caching

**Choose when:** reads are slow and repeated.

**Costs:** one invalidation handler per event that changes a cached answer; a short window (the entry's lifetime, 60 s by default) in which a concurrent read can be stale; Redis for several processes. Built and proved in the C# advanced reference solution (decision 52).

### scheduler

**Choose when:** recurring jobs run on a clock.

**Costs:** a worker host, a table of runs per tick, a service user per job; PostgreSQL only. Built and proved in the C# advanced reference solution (decision 52).

**Rules:** R9.

### storage

**Choose when:** files are stored or served.

**Costs:** a storage service (S3 or MinIO; local disk in development), size and type limits, orphan files to clean up. Written but never built or proved, and in no preset (decision 53).

### secrets

**Choose when:** values such as tax numbers or third-party API keys must be unreadable in the database and its backups.

**Costs:** keys to keep in a secret store and rotate; secret columns cannot be searched. Built and proved in the C# advanced reference solution (decision 52).

### loop-guard

**Choose when:** handlers could trigger each other forever.

**Costs:** a chain of command names on the context and on every event; a 500 `loop_detected` when it grows past 16. Built and proved in the C# advanced reference solution (decision 52).

### contexts

**Choose when:** groups of aggregates stop sharing a vocabulary.

**Costs:** a `Contexts/{Context}` segment in every path, communication by events and ids only, a rule test; wire names follow the new namespaces. Written but never built or proved, and in no preset (decision 53): when a developer chooses it, say so before writing `MORTIS.md`.

### events-project

**Choose when:** another process needs the event contracts without the domain.

**Costs:** a `{Name}.Contracts` project (packable) to version; renaming a contract member breaks consumers. Built and proved in the C# advanced reference solution (decision 52).

### idempotent-commands

**Choose when:** clients retry POST requests (mobile clients, payment flows, queues that redeliver).

**Costs:** an `Idempotency-Key` header, a table of seen keys with their responses (24 hours by default), and a cleanup. A crash between the commit and storing the response can still run a retry twice after the in-progress timeout. Written but not generated yet (`architecture/http-and-operations.md`, decision 48); PostgreSQL only.

### observability

**Choose when:** the service runs in production and someone must see why a request was slow or an event never arrived.

**Costs:** OpenTelemetry packages in the hosts, a column on the outbox row for the trace context, `/health/live` and `/health/ready`, a collector to run when the OTLP endpoint is set (nothing is exported by default). Written but not generated yet (decision 48).

### api-conventions

**Choose when:** a front end or another team consumes the API.

**Costs:** one error body for every failure (binding and routing included), unknown request members refused with a 400 (opt out per command), `PUT` replaces and `PATCH` with `Optional<T>`, enums as strings, one list shape. No API versioning. Written but not generated yet (decision 48).

Example:

```
| Key | Value | Why |
| addons | correlation, api-conventions | traceable requests, a front end |
```
