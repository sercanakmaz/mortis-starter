# Events

Which handler kind to pick, how events leave the process, and what each delivery model can lose. The wire format of messages is not here; it lives in the messaging pages of the chosen language.

## Sync handler or event consumer

| | Sync handler | Event consumer |
|---|---|---|
| Runs | before `COMMIT`, in the command's transaction | after `COMMIT`, in a new scope and its own transaction |
| If it fails | the whole change is rolled back | logged; the change stays |
| Can veto | yes | no |
| Consistency | strong: all or nothing | eventual: the next read may not show it yet |
| Right for | a rule that must hold together with the change (a blocked customer vetoes an order; count a completed order on the customer) | stock, read models, notifications, anything slow or allowed to lag |

Choose a **sync handler** when the change must not exist without the reaction. Choose an **event consumer** when the reaction may lag or fail without undoing the change. When unsure, ask: "if this reaction fails, should the user's action fail?" Yes → sync.

## How many of each

| Relation | Count | Consequence |
|---|---|---|
| command → handler | one | exactly one handler runs a command |
| handler → aggregates changed | one, or several when the action is about all of them | a *consequence* of the action is an event handler, not another line in the command handler |
| operation → events raised | **N** | one command can raise several events: `OrderCompleted`, `StockReleased`, `LoyaltyPointsEarned`; each is a separate fact |
| event → handlers | **N** | any number of aggregates may react; none knows of the others |
| handler → commands sent | one | a second reaction is a second handler, not a second line in the first |
| command → events, counting reactions | grows | one request can fan out into many commands and events; all share one correlation id and each records what directly caused it |

So "one command, one event" is wrong in both directions: a command raises as many events as the facts it created, and an event is reacted to by as many handlers as there are interested aggregates.

## Where a reaction lives

A reaction to another aggregate's event is a handler or consumer in the reacting aggregate's folder, and it sends that aggregate's own command. It never calls the service of the aggregate that raised the event.

## Registering an event consumer

Every event consumer is registered by hand, one line each, in the host that runs it, with its own settings:

```
host registration:
  addEventConsumer(DecreaseStockWhenOrderLineAdded, endpoint => endpoint.retry(3))
  addEventConsumer(IncreaseStockWhenOrderLineCancelled, endpoint => endpoint.retry(3).named("stock-increase"))
```

- Nothing is discovered: a consumer without its line never runs.
- `retry(n)` is the consumer's number of retries after the first attempt, in every publishing model; there is no global retry setting. `named(queue)` matters only with a broker.
- The host is the one that runs the consumers: the API and the console without a broker (they publish or write the rows), the worker with a broker.

## The three publishing models

An event consumer, and anything outside the process, hears of a queue event through one of three publishing models. The model is chosen once per project (`events.outbox`), and the project contains only that model's code.

| Model | What happens | What can be lost |
|---|---|---|
| `none` | after `COMMIT`, a publisher in the same process runs the event consumers directly | an event whose consumers had not run when the process died |
| `background` (publish in the command) | the command writes an outbox row per event in its own transaction; a worker publishes the rows | nothing; publishing waits up to one polling interval |
| `after-command` (publish after the command) | the same rows; right after `COMMIT` the request publishes its own rows; the worker sweeps and retries what is left | nothing |

In both outbox models the row is written in the command's transaction, so "the change happened" and "the event will be handled" are one fact. Choose `after-command` when the caller should see the consequences when it gets its answer; `background` when the request should do nothing more than its own change.

## Outbox write

```
commit(transaction):
  save the aggregates
  run the sync handlers                          they may raise more events
  for each queue event raised in this transaction:
    for each registered consumer of the event:                    every event consumer is a consumer of its own
      outbox.add(eventId: event.id, consumer, payload: serialize(event), attempts: 0, maxAttempts: consumer.retry + 1, nextAttemptAt: now)
    (with a broker: one row per event; the broker gives each consumer its own queue)
  save
  COMMIT
  after-command only: for each of these rows: worker.publish(row.id)
```

## Worker

```
publish(claim):
  BEGIN
  row := claim()                                 next due row, or one row by id; "for update skip locked"
  if none: ROLLBACK; stop
  try
    run the row's event consumer                  in this transaction; events it raises become rows
    delete row
    COMMIT
  on failure
    ROLLBACK
    row.attempts += 1; row.nextAttemptAt := now + 2^attempts seconds; row.lastError := the error
    at the row's maxAttempts it stays for an operator (with a broker, publishing retries until the broker takes it)

loop every polling interval:
  repeat publish(next due row) until nothing is due
```

- One row, its consumer and its deletion are one transaction: a row is handled once or not at all, so no inbox is needed. A failing consumer never blocks or repeats another consumer of the same event.
- `skip locked` lets two workers, or a request and a worker, run side by side; each row goes to one of them.
- Rows are taken in the order they happened; a retry can put a later event first.
- An operator retries a row that reached the limit by resetting its attempts.
- With a broker (`events.transport` = `rabbitmq`) the worker sends the row to the broker and deletes it only after the broker confirmed it. A receive endpoint in the worker host consumes it:

```
one receive endpoint per registered consumer, its queue named after the consumer:
receive(message):
  not the consumer's event: acknowledge and log
  repeat up to the consumer's retry + 1:
    BEGIN
    run the consumer
    COMMIT; stop
  still failing: publish the message to "<endpoint>_error" with the fault headers
  acknowledge
```

- A broker delivers at least once and the bus does not deduplicate: every event consumer must be safe to run twice. A consumer that cannot be keeps its own record of what it handled, in its own transaction.
- The wire format (envelope, exchange names, fault headers) is MassTransit 8's, so services built on it and ours exchange events both ways.

## Correlation and causation

Every command and event carries both:

- **correlation**: the id of the whole chain that started with one outside request. It never changes along the chain.
- **causation**: the id of the message that directly caused this one. A command's causation is the id of the event that triggered it; an event's causation is the id of the command that raised it.

An event copies its command's correlation, and a command sent by a handler copies the event's correlation, so one search by correlation finds everything one request caused.

How they get there, without a handler doing anything:

```
host fills command.context (actor, correlation)
pipeline completes it (a new correlation when absent) and marks the command as running
while it runs, every event its aggregates raise is stamped with its correlation, causation, tenant, language
an event handler sends its command with context := from(event):
    correlation := event.correlation, causation := event.id, actor := event.actor (sync) or system (async)
a command sent inside a running one is marked running on top of it; its events get its own context; the outer one is restored after
```

The log line of every command and every published event shows its correlation id (and its causation id). Where the correlation id of a request comes from is the `correlation` add-on's choice: without it, a new id per request; with it, the caller's `X-Correlation-Id` when it sends a valid one, returned in the response.
