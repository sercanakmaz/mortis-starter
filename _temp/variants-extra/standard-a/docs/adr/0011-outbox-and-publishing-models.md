# 0011 — Events that leave the transaction go through an outbox

- Status: accepted
- Date: 2026-11-20
- Context: An event consumer runs after the command's transaction has committed (0004). If the process dies in between, the event is lost and the change it should have caused never happens. This project must not lose events.

## Decision

Every queue event is written to `outbox_messages` by the unit of work, in the command's own transaction, after the sync handlers have run: one row per registered event consumer, because every event consumer is a consumer of its own. `OutboxWorker`, a background service, takes the next due row with `for update skip locked`, runs that row's consumer and deletes the row, all in one transaction. A failure rolls back, counts the attempt and waits `2^attempts` seconds; the row's attempt limit is its consumer's `Retry(n)` + 1, and a row that reaches it stays for an operator.

This project publishes `after-command`:

- `background` (publish in the command): the command only writes rows; the worker publishes them within a polling interval.
- `after-command` (publish after the command): right after `COMMIT` the request publishes its own rows; the worker sweeps and retries what is left.

Only the chosen mode's code is in the project.

## What varies

When an event is published after the change that raised it.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Publish in process after `COMMIT` (`none`) | Nothing | An event is lost when the process dies before its consumers run |
| Publish in the transaction, before `COMMIT` | Nothing | A rolled-back command would have published its events |
| An outbox plus an inbox and a separate send step | A second table, a lock column, idempotent handlers | More moving parts than one transaction per row |
| **One table; the row, its consumer and its deletion in one transaction** | One table, a worker host | n/a |

## Consequences

- Gained: "the change happened" and "the event will be handled" are one fact; a row is handled once or not at all.
- Lost or owed: a slow handler holds its row's lock; a retry can reorder two events; renaming an event type strands its pending rows until they are published.
- Cost of going back: medium; the table and the worker go, the handlers stay.

## How it is enforced

`OutboxMessageTests`, and the conformance checks in `outbox.sh`: atomicity, deletion, failure, context and latency.
