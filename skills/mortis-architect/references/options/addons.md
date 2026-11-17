# Add-ons

Optional parts, each adding files and registration lines. Choose several; the value is a comma-separated list.

## addons

The add-ons switched on.

**By preset:** lean `none`, standard `correlation, api-conventions`, advanced `correlation, soft-delete, caching, scheduler, secrets, loop-guard, contexts, events-project, idempotent-commands, observability, api-conventions`.

### correlation

**Choose when:** every log line and event must be traceable to one request.

**Costs:** a header in, a field on every command and event.

### soft-delete

**Choose when:** deleted data must stay for audit or undo.

**Costs:** a filter on every query; unique indexes must ignore deleted rows.

### caching

**Choose when:** reads are slow and repeated.

**Costs:** invalidation by handlers; stale reads possible.

### scheduler

**Choose when:** recurring jobs run on a clock.

**Costs:** a worker host and a lock so one instance runs a job.

**Rules:** R9.

### storage

**Choose when:** files are stored or served.

**Costs:** a storage service and a port to keep behind an interface.

### secrets

**Choose when:** values such as keys are stored encrypted.

**Costs:** a key to manage and rotate.

### loop-guard

**Choose when:** handlers could trigger each other forever.

**Costs:** a depth counter on the context; a failure when it is exceeded.

### contexts

**Choose when:** groups of aggregates stop sharing a vocabulary.

**Costs:** folders per context and communication by events and ids only.

### events-project

**Choose when:** another process needs the event contracts without the domain.

**Costs:** a separate project to version.

### idempotent-commands

**Choose when:** clients retry POST requests.

**Costs:** an idempotency key and a table of seen keys.

### observability

**Choose when:** traces, metrics and health checks are needed in production.

**Costs:** a collector to run and settings to keep.

### api-conventions

**Choose when:** a front end consumes the API.

**Costs:** one error body, one list shape, OpenAPI; conventions to follow.

Example:

```
| Key | Value | Why |
| addons | correlation, api-conventions | traceable requests, a front end |
```
