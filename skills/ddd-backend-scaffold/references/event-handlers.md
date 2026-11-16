# Sync and async event handlers

Read this before adding a handler, or when someone asks why an effect shows up late, twice or not at all. The mechanics are in `assets/csharp/docs/architecture.md` §3; this file is the decision guide and the worked example.

## Choose by one question

> If the reaction fails, must the original change fail too?

- **Yes** → sync handler. It runs inside the transaction and a throw rolls everything back.
- **No, it may lag and may fail on its own** → async handler. It runs after COMMIT and a throw is only logged.

| You want | Use | Why |
|---|---|---|
| A state rule that must hold together with the change (the order is `Cancelled` the moment its last line is) | sync | the next read must never see a half-done state |
| A veto (a blocked customer cannot complete an order) | sync | only a throw inside the transaction can undo the change |
| Stock moves, read models, e-mails, search indexing | async | the order must not fail because a counter did |
| Calling another system | async, and later through the outbox | never inside a database transaction |

## The worked example

One event, two handlers of different kinds. `Order.CancelLine` raises `LineCancelled(Id, LineId, Sku, Quantity, Reason)` and knows nothing about either.

| Handler | Folder | Kind | Effect |
|---|---|---|---|
| `CancelOrderWhenOrderLineCancelledEventHandler` | `Application/Orders/` | sync | runs `CancelOrderWhenAllLinesCancelledCommand`; the order becomes `Cancelled` in the same transaction when no active line is left |
| `IncreaseStockWhenOrderLineCancelledEventHandler` | `Application/Products/` | async | runs `IncreaseProductStockCommand(sku, quantity)` after the commit |
| `DecreaseStockWhenOrderLineAddedEventHandler` | `Application/Products/` | async | reacts to `LineAdded`; runs `DecreaseProductStockCommand(sku, quantity)` |
| `RegisterCompletedOrderWhenOrderCompletedEventHandler` | `Application/Customers/` | sync | runs `RegisterCompletedOrderCommand`; a blocked customer throws and vetoes the completion |

Run `dotnet run --project src/<Name>.Console -- stock-flow` to watch it: stock goes down on the order, up on a cancel, the order is `Cancelled` after the last line, and an oversold order stays open while the failed decrement is logged.

## Adding a handler

1. Raise the event inside the aggregate method that changes the state (rule 3). Put on the record every value the handler needs, as strings and numbers. A handler never reloads the aggregate that raised the event just to read what the event could have carried.
2. For an async handler, make the event record implement `IQueueEvent`. The compiler enforces this: `BaseAsyncEventHandler<TEvent>` constrains `TEvent` to it.
3. Add the command that does the reaction, in the reacting aggregate's Application folder (`IncreaseProductStockCommand`-style: a record, a validator when it has input to check, a handler that calls one service method). It implements `ITransactionalCommand`.
4. Put the event handler next to it, named `<Action>When<Aggregate><Event>EventHandler`, with a primary constructor taking `ICommandDispatcher`. The body is one awaited `commandDispatcher.Send(new ...Command(...))` call (rules 8, 9 and 10). Add the service method and the aggregate method that holds the rule if they do not exist.
5. Register the command handler (`AddCommandHandler<TCommand, TContent, THandler>()`), its validator and the event handler in `ApplicationModule`: `services.AddScoped<BaseSyncEventHandler<LineCancelled>, ...>()` or `services.AddScoped<BaseAsyncEventHandler<LineAdded>, ...>()`. Nothing is found by scanning, so an unregistered handler silently does nothing.
6. Test the handler with a dispatcher (`CommandDispatcherBuilder`) over the real command handler and a service built from a fake repository (the example's `Application/.../*EventHandlerTests`), the aggregate rule in the aggregate's tests, and the whole path with a Console scenario against PostgreSQL.

## What to know before relying on it

- **Why Application, not Domain.** A handler in the domain can only call a service. In Application it calls a command, so the reaction is validated, logged and transactional exactly like a request, and the same command can also serve an endpoint if one is ever needed.

- **A command result is mapped before COMMIT.** `PUT .../lines/{id}/cancel` answers with the order as the command left it; the sync handler runs afterwards, so the response can still say `Open` while a `GET` says `Cancelled`. If a caller needs the final state, it reads again.
- **An async handler cannot veto.** The oversold order in `stock-flow` stands and the failed decrement is only logged. If the business cannot accept that, the check belongs in a sync handler or in the aggregate.
- **Async handlers run once now, at least once later.** Today `AsyncEventHandlerDispatcher` calls them in-process, awaited, right after the commit. When a broker and an outbox replace it, a handler can run twice. Write handlers so that a second run is harmless, or record processed event ids.
- **Contended rows need a concurrency token.** Two async handlers decrementing the same product would each read the old stock and one write would be lost. `Product` maps PostgreSQL's `xmin` as a row version, so the second writer gets a `DbUpdateConcurrencyException`, and the dispatcher retries that handler in a fresh transaction (`MessagingConstants.MaxAttempts`, with a short growing pause). The smoke test sends 8 concurrent orders for a product with stock 8 and expects 0 left.
- **The audit user of an async handler is `system`** (`SystemUser.Id`), because the request that raised the event has already finished. The handler puts it on the command it builds. A sync handler passes the event's own user (`@event.UserId`) instead.
- **Events chain.** A handler's own writes raise events that are handled too: sync ones inside the same transaction, async ones after its commit. A cycle between two aggregates loops forever, so draw the reactions before adding the second direction.
- **Sync handlers run in `Sequence` order** (default 0). Give a handler a `Sequence` only when it must run after another one; do not rely on registration order.
