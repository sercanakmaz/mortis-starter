# Scenarios

Each scenario walks one situation block by block. Every numbered step starts with the layer that owns it in square brackets. After the steps comes the wrong placement people usually pick. Layer tags: Host, Application, Domain, Infrastructure, Shared.

## 1. Completing an order updates the customer (sync)

1. [Host] parses the request into `CompleteOrder` and fills the context.
2. [Shared] the pipeline validates and begins the transaction.
3. [Application] the handler loads the order for update.
4. [Domain] the order checks it is open and has lines, becomes completed, raises `Completed`.
5. [Application] a sync handler of `Completed` sends `CountCompletedOrder` to the customer.
6. [Domain] the customer aggregate adds the order to its count.
7. [Shared] the pipeline commits; both changes exist or neither.

**Wrong placement:** the complete-order handler also loading the customer and incrementing it. Counting is a consequence: the order is completed without it, so it is an event handler, and the order handler does not need to know customers count orders.

## 2. Adding a line moves stock (async)

1. [Host] parses `AddLine` and fills the context.
2. [Application] the handler loads the order and calls its operation.
3. [Domain] the order adds the line and raises `LineAdded`.
4. [Shared] the pipeline commits the order.
5. [Infrastructure] after `COMMIT` the in-process event dispatcher runs the async handler in a new scope.
6. [Application] the async handler sends `ReserveStock`.
7. [Domain] the product reduces its stock.

**Wrong placement:** reducing stock inside the order operation. A stock failure would then undo a line the customer already added, and the order would know about products.

## 3. A blocked customer vetoes an order (sync, rollback)

1. [Host] parses `CreateOrder`.
2. [Application] the handler creates the order.
3. [Domain] the order is created and raises `Created`.
4. [Application] a sync handler of `Created` sends `CheckCustomerAllowed`.
5. [Domain] the customer aggregate refuses: it is blocked, and raises a business failure with code `customer.blocked`.
6. [Shared] the failure reaches the pipeline, which rolls back the order.
7. [Host] returns the failure's code and status.

**Wrong placement:** an `if customer.blocked` in the order handler. The rule then lives in Application and the next entry point forgets it.

## 4. Stock runs out under concurrency

1. [Host] two requests each add a line for the last unit.
2. [Domain] each product operation checks the stock it loaded.
3. [Infrastructure] the repository detects that the row changed since it was read, via the concurrency token, and raises a conflict.
4. [Infrastructure] the async-handler runner retries the losing attempt in a fresh transaction.
5. [Domain] on the retry the product sees stock 0 and refuses with `stock.insufficient`.
6. [Application] the failure is logged for an async handler, or returned for a sync one.

**Wrong placement:** a lock in the handler or an in-memory counter. Storage detects conflicts; the aggregate decides what a conflict means.

## 5. An import of a thousand rows

1. [Host] a Worker or console run reads the file.
2. [Host] for each row it opens a new scope and sends one `ImportRow` command.
3. [Shared] each row validates and gets its own transaction.
4. [Application] the handler creates or updates the aggregate.
5. [Domain] the aggregate accepts or refuses the row.
6. [Host] failures are collected per row; successes are not undone by later failures.

**Wrong placement:** one command with a thousand rows in one transaction. One bad row would undo all, and the transaction would be long.

## 6. A rule that needs another aggregate's data

Placing an order may not exceed the customer's credit limit, which lives on the customer.

1. [Application] the `PlaceOrder` handler loads the new order's lines and the customer.
2. [Domain] the order cannot decide alone; the handler asks a domain service, `CreditPolicy`, whether the order fits the customer's credit.
3. [Domain] the domain service asks a read port for the customer's open order total (the rule decides it needs that) and decides; it refuses with `credit.exceeded`. It saves nothing.
4. [Application] the handler creates the order only when the policy allows it.
5. [Shared] the pipeline commits.

When the rule reads only a value that changes rarely (a customer's tier), a copy on the order, kept up to date by an event handler, avoids loading the customer at all.

**Wrong placement:** the handler loading both and deciding with an `if` of its own. The decision is business logic; it belongs in the domain service, where it can be tested and reused.

## 7. A notification after a change

1. [Domain] the aggregate raises `Shipped`.
2. [Shared] the pipeline commits.
3. [Infrastructure] the dispatcher runs the async handler.
4. [Application] the handler sends `NotifyCustomer`.
5. [Infrastructure] the notification adapter calls the outside service.
6. [Infrastructure] if the call fails, the retry policy tries again; the shipment stays committed.

**Wrong placement:** sending the email inside the shipping operation or in a sync handler. A mail server outage would then block shipping.

## 8. A report query

1. [Host] a report request arrives with filters.
2. [Application] a query (not a command) is sent through the dispatcher.
3. [Infrastructure] the query implementation reads storage directly and returns flat rows.
4. [Application] the handler maps rows to a view.
5. [Host] returns the view.

**Wrong placement:** loading every aggregate and summing in memory. A report is a read; it needs no rules and no aggregate.

## 9. A message arrives twice

1. [Host] a Worker consumes a message from the broker.
2. [Infrastructure] the inbox looks up `(message id, handler)`; it has been seen.
3. [Host] the Worker acknowledges and does nothing.
4. [Domain] nothing changes; the first run already did the work.

**Wrong placement:** relying on the broker to never repeat. Delivery is at-least-once; the consumer protects itself.

## 10. The process dies between commit and publish

1. [Shared] the pipeline commits the change and writes the outbox row in the same transaction.
2. [Infrastructure] the process dies before sending.
3. [Infrastructure] after restart the relay finds the pending row and publishes it.
4. [Infrastructure] if it died after sending but before marking the row, the row is sent again and the consumer's inbox skips the repeat.

**Wrong placement:** publishing straight after the commit with no stored row. A crash in that gap loses the message forever.

## 11. One command raises several events

1. [Host] parses `CompleteOrder` and fills the context.
2. [Shared] the pipeline validates and begins the transaction.
3. [Application] the handler loads the order and calls `complete`.
4. [Domain] the order operation changes its state and raises two events: `Completed` and `DiscountApplied`.
5. [Application] a sync handler of `Completed` sends `CountCompletedOrder`; a second handler of `Completed`, in the stock folder, sends `ReleaseReservation`.
6. [Application] an async handler of `DiscountApplied` sends `RecordLoyaltyPoints`.
7. [Shared] the pipeline commits; the async handler then runs, and the outbox row for each queue event is written with the same correlation id.

**Wrong placement:** recording loyalty points in the complete-order handler. The order is completed without them, so they are a consequence: an event handler.

## 12. A transfer changes two aggregates in one command

Stock moves from one warehouse to another.

1. [Host] parses `TransferStock` and fills the context.
2. [Shared] the pipeline validates and begins the transaction.
3. [Application] the handler loads both warehouses for update.
4. [Domain] the source warehouse releases the stock, refusing with `stock.insufficient` if it has too little; the target receives it. Each raises its own event.
5. [Application] the handler saves both.
6. [Shared] the pipeline commits; both change or neither does.

The second aggregate belongs in the command handler here: without the receiving side the command is not a transfer, a rule spans both, both are known up front and live in the same database.

**Wrong placement:** an async handler that receives the stock after `Released`. If it fails, stock has left one warehouse and arrived nowhere.

## 13. A decision that needs another system

An order's tax depends on rates from an outside tax service.

1. [Application] the `QuoteOrder` handler loads the order.
2. [Domain] the handler asks the domain service `PricingService` for the total.
3. [Domain] the domain service asks the `TaxRates` port, declared in Domain, for the rate of the shipping country.
4. [Infrastructure] the anti-corruption layer calls the tax service and translates its answer into a `Percentage`.
5. [Domain] the domain service computes the total; the order records it.
6. [Application] the handler saves and maps the view.

**Wrong placement:** the handler calling the tax service's client and passing its response type into the order. The outside model now lives in Domain and every change on their side breaks ours.
