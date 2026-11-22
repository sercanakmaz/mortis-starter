# The query side

How a read reaches its data. With `queries` = `commands` (the core) a read is a command that loads an aggregate and maps it to a view. `split` gives reads their own types; `read-models` adds tables shaped for one screen. The code of one language is in its pages (`queries.md`, `read-models.md`).

## split

```
query      := { context, fields }                  carries the caller's context like a command; changes nothing
handler(query):
  validate(query)                                  a failure is validation_failed, before any read
  view := reader.find(...) | reader.list(filter)   a read-only port next to the repository
  return map(view)
reader:    a projection straight to the view in the database: no tracking, no aggregate loaded,
           filter, sort and page inside one statement, plus one count for the total
```

- A query has no transaction, raises no event, and never depends on the unit of work, a repository or a domain service. An architecture test checks it.
- The reader port and its view records sit next to the repository in the domain layer, because the persistence adapter points only at the domain and the shared kernel.
- A list query has typed fields (`page`, `size`, a closed `sort` set, the filters that screen needs); there is no generic filter language. Every sort ends on a unique column, so pages never overlap.
- The tenant filter and the caller's language apply to queries exactly as to commands.
- The hosts send queries through their own dispatcher; the console runs them by name like commands.

## read-models

```
a read model   := a table shaped for one screen, owned by the persistence adapter
on every event of its aggregate (a sync handler, inside the command's transaction):
  refresh(id) := recompute the whole row from the aggregate's tables
rebuild        := delete every row; recompute all of them, in one transaction (a console command)
```

- A read-model handler writes only its read model, never an aggregate; it is the one kind of event handler that does not send a command.
- Recomputing the row instead of applying each event as a change means a missed or repeated event cannot leave it wrong.
- Updated by sync handlers, a read model is always consistent with the write. Updated by event consumers after the commit, it is eventually consistent and needs an outbox (R10).
- Data copied from another aggregate (the customer's name on an order summary) is as fresh as the last change to the read model's own aggregate, unless that other aggregate's events refresh it too.

## Not offered

GraphQL, OData, a separate read database, full-text search.
