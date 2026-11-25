# Advanced add-ons

The add-ons a project chooses when it grows: what each adds, in the notation of `pseudo-code.md`, and the rule it must keep. The code of one language is on its own page (`soft-delete.md`, `caching.md`, `scheduler.md`, `storage.md`, `secrets.md`, `loop-guard.md`, `contexts.md`, `events-project.md`). Six are built and proved in the C# advanced reference solution (decision 52); `storage` and `contexts` are knowledge only, and `contexts` is in no preset (decision 53).

## soft-delete

```
aggregate : ISoftDeletable     Delete(user) / Restore(user), each raising an event
every query                    filtered: not deleted (a named filter, so the tenant filter stays when it is ignored)
natural unique keys            unique among rows not deleted, so a deleted key can be reused;
                               a key another table references keeps a real unique constraint and stays taken
```

- Delete is a state change, not a removal: audit and undo keep working. Restore can fail when the key was reused meanwhile (409 `duplicate`).

## caching

```
read handler   cache.getOrCreate(key = plural:tenant:id:language, tag = aggregate:tenant:id, load)
on every event that changes what a cached answer shows: a sync handler removes the tag
```

- Only answers are cached, never the aggregate a command loads: a stale cache never feeds a write.
- Removal happens inside the command, before the commit; the entry's lifetime bounds the short window in which a concurrent read can put the old answer back.

## scheduler

```
job := { name, cron, run(context) -> send commands }
every poll, per job: tick := latest due occurrence; insert (job, tick) on conflict do nothing; the winner runs it
```

- A job holds no rule; it is the clock's controller. It runs as a configured service user, never a hidden system user (decision 47).
- The tick, not the replica, is the unit of work, so any number of workers run each tick once (R9: a worker host is needed).

## storage

```
IFileStorage: put(key, content, type) / open(key) / delete(key)       S3-compatible or local disk behind it
key := plural/tenant/id/name, built from ids the application owns
write the file first, then the row that points at it
```

- The aggregate holds the key, never the bytes. A failure leaves at worst an orphan file, never a row pointing at nothing.

## secrets

```
protect(value)   := v1.keyId.wrap(dataKey).nonce.encrypt(dataKey, value).tag     a fresh data key per value
unprotect(text)  := the key named inside opens the data key, the data key opens the value
```

- The domain sees plaintext; the database and its backups see envelopes. Rotation adds a key and makes it current; old envelopes still open.
- A secret column cannot be searched by value.

## loop-guard

```
command context.chain := the commands that led here; events carry it; reactions inherit it
pipeline: chain := chain + this command; length > max -> loop_detected (500), naming the chain
```

- Counting commands is enough: every reaction runs a command. The chain crosses the outbox and the bus inside the event.

## contexts

```
Domain/Contexts/{Context}/Aggregates/{Plural}, Application/Contexts/{Context}/{Plural}, ... mirrored everywhere
context A refers to context B only through B's events and ids
```

- One process and one database: a context is a boundary in the code. A test forbids signatures that cross it; the review skill catches references inside method bodies.

## events-project

```
{Name}.Contracts := the queue events only; references Shared only; Domain references it
another service references Contracts, never Domain; wire names from the namespace or pinned by name
```

- Only events that leave the process move; renaming a member of one is a breaking change for every consumer.

## Not offered yet

Ordered handlers are already in the core (`Sequence` on sync handlers). Feature flags, e-mail, real-time push and sagas are written when a project asks (step 18's table); Kafka, event sourcing, gRPC, GraphQL and a database per tenant are left out.
