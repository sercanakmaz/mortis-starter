# Aggregates as documents

How the building blocks map onto a document database (`database` = `mongo`). Nothing above the persistence adapter changes: the Domain and Application code is the same as with a relational database. The code of one language is in its pages (`infrastructure-mongo.md`).

## The mapping

```
aggregate            one document in the aggregate's collection (snake case, plural)
child entity         embedded in its aggregate's document
value object         embedded where it is used
reference            the other aggregate's id; no foreign key
unique rule          a unique index (tenant first with tenancy)
concurrency          a version number on the document, +1 on every replace
```

```
repository.findTracking(id):
  document := collection.find(_id = id)
  remember(aggregate, document.version)
repository.update(aggregate):
  replace where _id = aggregate.id and version = remembered   -> set version + 1
  nothing matched: concurrency conflict (the same failure the relational repository raises)
unit of work:
  begin: start a session and a multi-document transaction
  commit: run the sync handlers, write the outbox in the same session, commit
```

## Rules

- The server runs as a replica set, a single node in development: transactions need it, and a standalone server fails at the first write inside one, not at connect. The checks verify the replica set before anything else.
- Every query goes through the repository base, so the tenant filter cannot be forgotten (there is no global filter); an architecture test enforces it.
- With no foreign keys, a reference to a missing aggregate is accepted by the database. When a rule needs the referenced aggregate to exist, the check is in a domain service.
- Unique indexes replace migrations. They are ensured at start in development and by a console command in deployment; ensuring an existing index does nothing.
- Ids are time-ordered GUIDs stored as standard UUIDs. `keys.default` cannot be an integer (R12); an aggregate whose key the domain fixes as an integer gets it from a counter document in the same transaction.
- A document shape change: a schema version on the document and an upgrade when an old document is read. There is no migration tool.

## Not offered

Sharding, change streams as the outbox relay, time-series collections.
