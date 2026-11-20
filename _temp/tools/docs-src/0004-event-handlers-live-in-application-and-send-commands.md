# 0004 — Event handlers live in the Application layer and send commands; sync and async are separate

- Status: accepted
- Date: <the date the project was generated>
- Context: A reaction to an event (the customer is updated when an order completes, stock moves when a line is added) must run a use case. A handler in the Domain layer would have called services directly and skipped validation, logging and the transaction.

## Decision

An event handler is in the Application layer, next to the commands, named `<Action>When<Aggregate><Event>EventHandler`. Its body is one awaited `commandDispatcher.Send(new ...Command {...})`. A **sync** handler (`BaseSyncEventHandler<T>`) runs inside the transaction after `SaveChanges` and before the commit; if it throws, the command that raised the event is rolled back. An **async** handler (`BaseAsyncEventHandler<T>`) runs after the commit in a new scope and a new transaction, and is retried on `DbUpdateConcurrencyException` (up to ten attempts). Registration is explicit in `ApplicationModule`.

## What varies

Whether the reaction must succeed together with the change (sync) or may follow it (async).

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Handlers in Domain calling repositories | None | Skips the command pipeline |
| Sync handlers only | One mechanism | A slow or failing side effect would block or roll back the main change |
| **Sync and async, both in Application, both sending commands** | Two base classes and an event publisher | n/a |

## Consequences

- Gained: a reaction gets validation, logging and the transaction like a request does; a sync handler can veto.
- Lost or owed: there is no outbox, so an async handler is lost if the process dies between the commit and its run; nothing stops two handlers triggering each other forever; an async handler on a contended row needs a concurrency token (`Product` uses `xmin`).
- Cost of going back: moderate.

## How it is enforced

The conformance checks `stock` (sync and async on one event) and `blocked-customer` (a sync veto rolls back), and the example's `*EventHandlerTests`.
