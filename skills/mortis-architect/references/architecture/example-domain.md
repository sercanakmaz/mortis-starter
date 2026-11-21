# The example domain

The worked example a project gets when `example` is `true`: orders, customers and products. It is specified once here, in roles, so every language builds the same thing and the conformance checks can judge it. Notation: [pseudo-code.md](pseudo-code.md).

## Aggregates

```
aggregate Customer(id: CustomerId)
  state: name (max 200), email (max 320, unique), isBlocked, completedOrderCount
  create(id, name, email, by)
    email := lower-case(trim(email))
    raise Created(id, email)
  operation block()
    isBlocked := true
    raise Blocked(id)
  operation registerCompletedOrder()
    require not isBlocked                      else Rule("customer_blocked")
    completedOrderCount := completedOrderCount + 1

aggregate Category(id: CategoryId, assigned by storage)
  state: name (max 100, trimmed, unique)
  create(name, by)                             raises nothing: the id is not known yet
  operation markCreated()                      called by the handler after the insert
    require id is assigned                     else a programming error
    raise Created(id, name)

aggregate Product(id: ProductId)
  state: sku (max 64, unique, referenced by order lines), name (max 200), stock (never below zero, also guarded by storage),
         categoryId (optional, references Category)
  create(id, sku, name, stock, by, categoryId)
    require stock >= 0                         else InvalidParameter("stock")
    raise Created(id, sku)
  operation increaseStock(quantity)
    require quantity > 0                       else InvalidParameter("quantity")
    stock := stock + quantity
    raise StockIncreased(id, sku, quantity, stock)          [queue]
  operation decreaseStock(quantity)
    require quantity > 0                       else InvalidParameter("quantity")
    require stock >= quantity                  else Rule("product_insufficient_stock")
    stock := stock - quantity
    raise StockDecreased(id, sku, quantity, stock)          [queue]
  concurrency: a row version, because event consumers change stock concurrently

aggregate Order(id: OrderId)
  state: orderNumber (max 64, unique), customerId (references Customer), status: Open | Completed | Cancelled,
         lines: list<OrderLine>, total = sum of price.amount * quantity over lines that are not cancelled
  create(id, orderNumber, customerId, by)
    status := Open
    raise Created(id, orderNumber, customerId)
  operation addLine(sku, price: Money, quantity) -> OrderLine
    require status == Open                     else Rule("order_not_open")
    require quantity > 0                       else InvalidParameter("quantity")
    require no active line has this sku        else Duplicate("OrderLine")
    require every line has price.currency      else Rule("order_mixed_currency")
    line := new OrderLine(ids.next(), id, sku, price, quantity)
    raise LineAdded(id, line.id, sku, quantity)              [queue]
  operation cancelLine(lineId, reason)
    require status == Open                     else Rule("order_not_open")
    line := the line with lineId               or throw NotFound("OrderLine")
    line.cancel(reason)
    raise LineCancelled(id, lineId, line.sku, line.quantity, reason)   [queue]
  operation cancelWhenAllLinesCancelled() -> changed
    if status != Open or some line is active: return false
    status := Cancelled
    raise Cancelled(id, orderNumber, customerId)             [queue]
  operation complete()
    require status == Open                     else Rule("order_not_open")
    require some line is active                else Rule("order_nothing_to_fulfil")
    status := Completed
    raise Completed(id, orderNumber, customerId)             [queue]

entity OrderLine(id, orderId, sku (references Product.sku), price: Money, quantity, isCancelled, cancellationReason (max 500))
  operation cancel(reason)
    require not isCancelled                    else Rule("order_line_already_cancelled")
    isCancelled := true; cancellationReason := reason

value Money(amount, currency)
  require amount >= 0                          else InvalidParameter("amount")
  require currency has three letters           else InvalidParameter("currency")
  currency := upper-case(trim(currency)), culture-invariant
```

`[queue]` marks an event that leaves the transaction (event consumers, outbox). The others are handled only by sync handlers.

Every operation that changes an aggregate is followed by stamping who changed it (`touch(by)`), so its events carry the actor.

## Commands

| Command | Writes | Does | Returns |
|---|---|---|---|
| `CreateCustomer(id, name, email)` | yes | creates a customer; the unique email is enforced by storage | customer view |
| `BlockCustomer(id)` | yes | `block` | customer view |
| `GetCustomer(id)` | no | reads | customer view |
| `RegisterCompletedOrder(id)` | yes | `registerCompletedOrder` on the customer | customer view |
| `CreateCategory(name)` | yes | creates the category, saves it, then `markCreated` and saves the event; the unique name is enforced by storage | category view |
| `GetCategory(id)` | no | reads | category view |
| `CreateProduct(id, sku, name, stock, categoryId?)` | yes | creates a product; an unknown category is refused by storage | product view |
| `GetProduct(id)` | no | reads | product view |
| `IncreaseProductStock(sku, quantity)` | yes | loads by sku, `increaseStock` | product view |
| `DecreaseProductStock(sku, quantity)` | yes | loads by sku, `decreaseStock` | product view |
| `CreateOrder(id, orderNumber, customerId, lines: list<(sku, price, currency, quantity)>)` | yes | creates the order and adds each line | order view |
| `AddOrderLine(id, line)` | yes | `addLine` | order view |
| `CancelOrderLine(id, lineId, reason)` | yes | `cancelLine` | order view |
| `CompleteOrder(id)` | yes | `complete` | order view |
| `GetOrder(id)` | no | reads | order view |
| `CancelOrderWhenAllLinesCancelled(id)` | yes | `cancelWhenAllLinesCancelled`; saves only when it changed | order view |

Validators: ids not empty; names, emails, skus and order numbers present and within their maximum length; an email well formed; stock not negative; quantities above zero; an order has at least one line; each line's price not negative and currency three letters.

Every command handler changes one aggregate: in this domain every second change is a consequence, so it is an event handler.

## Reactions

| Event | Kind | Handler sends | Runs as | Why this kind |
|---|---|---|---|---|
| Order `Completed` | sync | `RegisterCompletedOrder(customerId)` | the user of the event | a blocked customer must veto the completion |
| Order `LineCancelled` | sync | `CancelOrderWhenAllLinesCancelled(orderId)` | the user of the event | the order must not stay open with no active line |
| Order `LineAdded` | async | `DecreaseProductStock(sku, quantity)` | the system actor | stock may lag; an order is not refused for stock |
| Order `LineCancelled` | async | `IncreaseProductStock(sku, quantity)` | the system actor | as above |

## Views

- Customer view: id, name, email, isBlocked, completedOrderCount.
- Category view: id, name.
- Product view: id, sku, name, stock, categoryId.
- Order view: id, orderNumber, customerId, status (as its name), totalAmount, lines; a line view: id, sku, price (the amount), currency, quantity, isCancelled, cancellationReason.

## Requests

| Request | Command | Answer |
|---|---|---|
| `POST /customers` | `CreateCustomer` | 201 |
| `GET /customers/{id}` | `GetCustomer` | 200 |
| `PUT /customers/{id}/block` | `BlockCustomer` | 200 |
| `POST /categories` | `CreateCategory` | 201, with the id the database assigned |
| `GET /categories/{id}` | `GetCategory` | 200 |
| `POST /products` | `CreateProduct` | 201 |
| `GET /products/{id}` | `GetProduct` | 200 |
| `POST /orders` | `CreateOrder` | 201 |
| `GET /orders/{id}` | `GetOrder` | 200 |
| `POST /orders/{id}/lines` | `AddOrderLine` | 200 |
| `PUT /orders/{id}/lines/{lineId}/cancel` | `CancelOrderLine` | 200 |
| `PUT /orders/{id}/complete` | `CompleteOrder` | 200 |

## Flows to try

There are no Console scenarios: each flow is a sequence of single commands, run from the Console or a script, and the conformance suite runs exactly these.

| Flow | Steps | What it proves |
|---|---|---|
| order | three products (stock 100), a customer, an order with two lines, add a third, cancel the second ("out of stock"), complete | children inserted and updated in place, a value object in two columns, a sync handler in the same transaction, async stock moves, audit columns |
| stock | bolt (10) and nut (5), an order of bolt x3 and nut x2, cancel the bolt line, cancel the nut line, then an order of bolt x20 | sync and event consumers on one event; an event consumer cannot veto |
| blocked-customer | a product, a customer, an order, block the customer, try to complete | a sync handler vetoes and the whole command rolls back |

Every write runs as the console actor; unique values get a random suffix so a flow can run twice.

## When tenancy is on

`Product`, `Customer`, `Order` and its `OrderLine` are tenant-owned; `Category` stays global. A sku is unique within a tenant, so two tenants may sell the same sku; an order line references the product with that sku in its own tenant, and an order its tenant's customer. Every flow above runs as one tenant's signed-in user.

| Flow | Steps | What it proves |
|---|---|---|
| tenants | the same sku in tenants A and B; A reads B's product; a caller without a tenant writes a product; both read a category; A orders B's only sku; orders of A and B interleaved | isolation (not found), 403 without a tenant, global data for all, the tenant-first foreign key, consumers that keep their event's tenant |

