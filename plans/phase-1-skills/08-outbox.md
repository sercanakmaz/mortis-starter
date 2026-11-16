# Phase 1, step 08 — The outbox (knowledge only)

Needs: phase 1 steps 04, 06. Size: large. Options: `outbox`, `transport-database`, `inbox`, `host-worker`. No C# is written, built or generated in this step.

## Goal

Offer exactly three delivery models for events that leave the transaction, chosen by `events.outbox`:

| Value | Behaviour | Can lose an event |
|---|---|---|
| `none` | Today's: after the commit the async handlers run in the same process (`events-inprocess`) | yes, if the process dies between the commit and the handler |
| `after-command` | The event is saved in the same transaction; right after the commit the same request publishes it and marks it published; a sweeper publishes what was left behind | no |
| `background` | The event is saved in the same transaction; a relay job polls and publishes | no; adds up to one polling interval of delay |

`after-command` and `background` are one module with one setting. This step also provides the `database` transport (the relay runs the handlers itself); RabbitMQ is step 09.

## Read first

`mortis-architect/references/architecture/events.md` and ADR 0004. In the existing C# solution `_temp/template/assets/csharp/` (temporary source material; never installed): `UnitOfWork.cs`, `EventContext.cs`, `EventHandlerExecutor.cs`, `AsyncEventHandlerDispatcher.cs`.

## Design

```
table outbox_messages
  id              = event id (primary key)
  type            wire name "<Namespace>:<TypeName>"
  payload         json
  occurred_at, correlation_id, causation_id, tenant_id, user_id, user_email, language
  status          pending | published | failed
  attempts, next_attempt_at, locked_until, published_at, last_error

table inbox_messages
  message_id, handler   (primary key together)
  processed_at
```

Write path (inside `CommitTransactionAsync`, after the sync handlers have finished and before COMMIT): every raised event that is an `IQueueEvent` becomes one outbox row. Sync handlers may raise more events; they are included.

Seam in the core: `IEventOutbox { Store(events); AfterCommit(eventIds) }` with a no-op default. `events-inprocess` keeps dispatching through `IEventDispatcher`; with the outbox module on, `IEventDispatcher` is not called by the unit of work at all.

Publish path:

```
publish(batch)
  claim rows: status = pending and next_attempt_at <= now and (locked_until is none or < now)
              ordered by occurred_at, id; limit N; "for update skip locked"; set locked_until
  for each row: transport.send(row)
     ok      -> status := published, published_at := now
     failure -> attempts += 1; next_attempt_at := now + backoff(attempts); last_error
                attempts >= max -> status := failed
```

- `after-command`: `AfterCommit(eventIds)` claims and publishes exactly those rows in the request, with a short timeout; failure is logged and left for the sweeper. The sweeper is the relay loop with a minimum age (default 30 seconds) so it does not race the request.
- `background`: `AfterCommit` does nothing; the relay loop runs every `PollingIntervalMilliseconds` (default 1000).
- The relay is an `IHostedService`. `hosts.worker` = `in-api` registers it in the Api; `separate` adds the `<Name>.Worker` project (module `host-worker`) and registers it only there.

`transport-database`: "send" means: open a new scope, restore the context from the row, begin a transaction, insert `(message_id, handler)` into the inbox, run the `BaseAsyncEventHandler<T>`, commit. A unique violation on the inbox insert means "already done": skip and report success. Each handler of an event is its own inbox row and its own transaction.

Operations: a cleanup job deletes `published` rows older than a retention period; Console commands `outbox-list-failed` and `outbox-retry <id>`.

Settings: `Outbox:Mode`, `PollingIntervalMilliseconds`, `BatchSize`, `MaxAttempts`, `SweeperMinimumAgeSeconds`, `RetentionDays`.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md` (`0007-outbox-and-delivery-models.md`).
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

Each item is a conformance check or an integration test that fails without the feature.

- [ ] **Atomicity:** a command that rolls back leaves no outbox row; a committed one leaves exactly one row per queue event.
- [ ] **No loss:** with `after-command`, make the transport fail once; the row stays `pending`, the request still succeeds, and the sweeper publishes it. With the process killed between COMMIT and publish (a test hook that exits), the next start publishes it.
- [ ] **Idempotency:** deliver the same row twice; the handler's effect (stock) happens once.
- [ ] **Two relays:** run two relay instances against one table for a few hundred events; every event is handled exactly once in effect and no row is claimed by both at the same time.
- [ ] **Retry and failure:** a handler that always throws ends in `failed` after `MaxAttempts`, with the error stored, and does not block later rows.
- [ ] **Context:** the handler runs with the event's correlation id, user and (when present) tenant.
- [ ] **Latency:** `background` delivers within two polling intervals in the conformance run; `after-command` before the HTTP response returns in the normal case.
- [ ] A project generated with `events.outbox` = `none` contains no outbox table, type or setting.

## Out of scope

RabbitMQ (step 09). Ordering guarantees per aggregate beyond "claimed in `occurred_at` order" (document that a retry can reorder). Change-data-capture relays.

## Traps

- The row must be written in the **same** transaction and the same connection as the aggregate. A second `DbContext` breaks the guarantee silently.
- `for update skip locked` only protects while the claiming transaction is open; that is why `locked_until` exists.
- The sync handlers run before the outbox write; an event raised by a sync handler's command must also be stored. Test it with the example's `LineCancelled` chain.
- `after-command` publishing must never fail the request. The change is committed; the caller must get its success.
