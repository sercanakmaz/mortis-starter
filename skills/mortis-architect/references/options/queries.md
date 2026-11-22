# Queries

How reads are separated from writes.

## queries

How a read reaches its data.

**By preset:** lean `commands`, standard `split`, advanced `read-models`.

### commands

**Choose when:** small projects: a read is a command that returns a view.

**Costs:** reads go through the write model and the pipeline; list screens load aggregates.

### split

**Choose when:** list and report screens that need their own shape.

**Costs:** a second set of types and a second kind of handler; a reader port and view records per aggregate that is read (`architecture/queries.md`). The C# pages are written but not built or run yet.

### read-models

**Choose when:** heavy reads, separate scaling, denormalised views.

**Costs:** a table per screen, refreshed by sync handlers in the same transaction (or by event consumers, eventually consistent, with an outbox), and a rebuild command. Includes everything `split` does.

**Rules:** R10.

Example:

```
| Key | Value | Why |
| queries | split | list screens need their own shape |
```
