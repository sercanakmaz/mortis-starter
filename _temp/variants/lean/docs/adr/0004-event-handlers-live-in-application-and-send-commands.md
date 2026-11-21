# 0004 — Event handlers and event consumers live in the Application layer and send commands

- Status: accepted
- Date: 2026-11-20
- Context: A reaction to an event (the customer is updated when an order completes, stock moves when a line is added) must run a use case. A handler in the Domain layer would have called services directly and skipped validation, logging and the transaction.

## Decision

A reaction is in the Application layer, next to the commands, and its body is one awaited `commandDispatcher.Send(new ...Command {...})`. A **sync event handler** (`<Action>When<Aggregate><Event>EventHandler : BaseSyncEventHandler<T>`) runs inside the transaction after `SaveChanges` and before the commit; if it throws, the command that raised the event is rolled back. It is registered in `ApplicationModule`. An **event consumer** (`<Action>When<Aggregate><Event>EventConsumer : BaseEventConsumer<T>`) runs after the commit in its own scope and transaction. It is registered with one line in the host that runs it, `AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(3))`, and `Retry(n)` is its number of retries in every publishing model. Nothing is found by scanning.

## What varies

Whether the reaction must succeed together with the change (sync) or may follow it (async).

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Handlers in Domain calling repositories | None | Skips the command pipeline |
| Sync handlers only | One mechanism | A slow or failing side effect would block or roll back the main change |
| **Sync handlers and event consumers, both in Application, both sending commands** | Two base classes, an event publisher and one registration line per consumer | n/a |

## Consequences

- Gained: a reaction gets validation, logging and the transaction like a request does; a sync handler can veto.
- Lost or owed: there is no outbox, so an event consumer is lost if the process dies between the commit and its run; a consumer without its line in the host never runs; nothing stops two reactions triggering each other forever; an event consumer on a contended row needs a concurrency token (`Product` uses `xmin`).
- Cost of going back: moderate.

## How it is enforced

The conformance checks `stock` (sync and async on one event) and `blocked-customer` (a sync veto rolls back), and the example's `*EventHandlerTests` and `*EventConsumerTests`.
