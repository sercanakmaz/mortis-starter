# Events

Which handler kind to pick, how events leave the process, and what each delivery model can lose. The wire format of messages is not here; it lives in the messaging pages of the chosen language.

## Sync or async

| | Sync handler | Async handler |
|---|---|---|
| Runs | before `COMMIT`, in the command's transaction | after `COMMIT`, in a new scope and its own transaction |
| If it fails | the whole change is rolled back | logged; the change stays |
| Can veto | yes | no |
| Consistency | strong: all or nothing | eventual: the next read may not show it yet |
| Right for | a rule that must hold together with the change (a blocked customer vetoes an order; count a completed order on the customer) | stock, read models, notifications, anything slow or allowed to lag |

Choose **sync** when the change must not exist without the reaction. Choose **async** when the reaction may lag or fail without undoing the change. When unsure, ask: "if this reaction fails, should the user's action fail?" Yes → sync.

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

A reaction to another aggregate's event is a handler in the reacting aggregate's folder, and it sends that aggregate's own command. It never calls the service of the aggregate that raised the event.

## The three delivery models

An event that must leave the process (to another service, to a broker) can be delivered in three ways. The model is chosen once per project.

| Model | What happens | What can be lost |
|---|---|---|
| `none` | events are handled in-process only; nothing is published | anything outside the process never hears of the change |
| `after-command` | after `COMMIT`, in the same request, the event is written to the outbox and sent to the broker straight away; if the send fails, the outbox row stays for a retry | nothing is lost once the outbox row is written; a crash between `COMMIT` and the send is covered because the row was written inside the transaction |
| `background` | the event is written to the outbox inside the command's transaction; a relay loop reads the pending rows and publishes them | nothing is lost; delivery is delayed by the relay's polling interval |

In both outbox models the write of the outbox row is part of the command's transaction, so "the change happened" and "the message will be sent" are one fact.

## Outbox write

```
inside the command's transaction, at commit:
  for each queue event raised:
    outbox.insert(
      id: event.id,
      type: event.wireName,
      payload: serialize(event),
      correlation, causation, tenant, actor, language,
      status: pending, attempts: 0, nextAttemptAt: now)
```

## Relay loop

```
loop:
  rows := claim pending rows where nextAttemptAt <= now, locked for this worker only
  for each row:
    try
      broker.publish(row.type, row.payload, headers from row)
      row.status := published
    on failure
      row.attempts += 1
      row.nextAttemptAt := now + backoff(row.attempts)
      if row.attempts exceeds the limit: row.status := failed
  release rows whose lock expired            (sweeper)
  wait the polling interval
```

Claiming uses a lock that other workers skip, so two relays never publish the same row.

## At-least-once and the inbox

Delivery is **at-least-once**: a message may arrive twice (a crash after publish, before the row is marked published). The receiver therefore records `(message id, handler)` in an inbox and skips what it has seen. Every async handler and every consumer is written so that running it twice has the same result as running it once.

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
