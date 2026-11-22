# The HTTP surface and operations

What every real service ends up adding to its API and to running it, done once and the same way everywhere. Three add-ons: `api-conventions` (the shape of requests and responses), `idempotent-commands` (a retried `POST` runs once) and `observability` (health, traces, metrics, request logs). The code of one language is in its pages (`api-conventions.md`, `idempotency.md`, `observability.md`). None of them is generated yet (decision 48).

## api-conventions

```
every failure            -> the error body { code, message, errors? }
  a command's exception      as today (ExceptionHandler)
  a body that does not parse -> 400 invalid_request, errors keyed by the JSON path
  an unknown member          -> 400 invalid_request, naming the member      (a command may opt out)
  an unknown route / method  -> 404 route_not_found / 405 method_not_allowed
a list                   -> { items, page, size, total }                     (PagedResult, the query side's type)
PUT  /things/{id}        replaces: every member is sent; a missing one is a validation failure
PATCH /things/{id}       changes the members present:
  member absent              -> Optional.None        leave it
  member: null               -> Optional.Of(null)    clear it (a validation failure if the member is required)
  member: value              -> Optional.Of(value)   set it
enums on the wire        as their names, never numbers
```

- The error body is the one the hosts already write; this add-on only makes the framework's own failures (binding, routing) use it, so a client parses one shape.
- Refusing unknown members turns a typo in a request (`nmae`) into a 400 instead of a silently ignored field. A command that must accept extra members (a form posted as is) says so on its own type; there is no global switch off.
- `PUT` on a sub-resource (`PUT /products/{id}/name`) replaces that sub-resource; intent commands stay the first choice for behaviour (`block`, `complete`). `PATCH` is for editing several plain members at once.
- `Optional<T>` lives in the shared kernel, so the Console's JSON arguments follow the same rule as HTTP bodies.
- No API versioning is generated (decision 48): one OpenAPI document. When a breaking change comes, add the version to the route then.

## idempotent-commands

```
POST with Idempotency-Key: k
  record := store.find(k, caller)
  record is none            -> store.start(k, caller, hash(body))  (a unique key: a racing twin gets 409 idempotency_in_progress)
                               response := run the request
                               a success ? store.finish(k, response) : store.forget(k)   (a failure frees the key)
  record.hash != hash(body) -> 422 idempotency_key_reused
  record is in progress     -> 409 idempotency_in_progress  (taken over after a timeout)
  record is finished        -> replay record.response, header Idempotency-Replayed: true
cleanup: delete records older than the retention period (24 hours by default)
```

- The key is scoped by the caller (user and tenant when there are any), so two clients cannot collide or read each other's responses.
- Only `POST` is covered: `PUT`, `PATCH` and `DELETE` are idempotent by their meaning. A `POST` without the header runs normally.
- The record is written outside the command's transaction. A crash after the command committed and before the response was stored leaves an in-progress record; a retry after the timeout runs the command again. The command's own rules (a unique order number) are the last guard.

## observability

```
GET /health/live      200 while the process runs
GET /health/ready     200 when every check passes, else 503 with each check's status:
                        database reachable; broker connected (events.transport = rabbitmq);
                        oldest pending outbox row younger than the threshold (events.outbox != none)
a trace per request:
  HTTP request span
    command span (name, outcome)                    CommandPipeline
      event handler span                            sync handlers
    outbox publish span      parent := the trace id stored on the outbox row
      consumer span          parent := the trace id carried in the bus envelope headers
metrics: command duration (by command, outcome); outbox pending count and oldest age; consumer failures
export:  OTLP when an endpoint is set; nothing by default
request log: method, route, status, duration, correlation id, trace id; never bodies
```

- One trace covers a request and everything it caused, across the outbox and the bus, with every transport: the trace context is stored with the outbox row and travels in the envelope headers.
- Every log line carries the trace id, so logs and traces join without a collector; with no exporter set the spans cost almost nothing.
- Readiness fails on a growing outbox, because a stuck publisher is invisible to a database check: commands still succeed while no event leaves.
- `/health` stays as the liveness answer every host already gives.

## Not offered

API versioning, rate limiting, dashboards, alert rules, log shipping, request bodies in logs.
