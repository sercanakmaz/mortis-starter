# Building blocks

One section per block. Each gives the purpose in one sentence, the pseudo-code, the rules, and the commonest mistake. Notation: [pseudo-code.md](pseudo-code.md). Where each block lives: [layers.md](layers.md).

## Entity

**Purpose:** a thing with an identity that stays the same while its data changes.

```
entity Entity(id)
  state: id, createdAt, createdBy, updatedAt, updatedBy
```

- Identity is assigned by the program before the entity is saved. The one exception is an aggregate whose key type is an integer the database assigns: its id exists only after the insert, so it raises its `Created` event in a separate operation the handler calls after saving (`markCreated`), never in its constructor.
- Two entities are the same when their ids are the same.
- **Commonest mistake:** letting the database generate the id; a new child that already carries an id is then mistaken for an existing row.

## AggregateRoot

**Purpose:** the one entity through which a cluster of entities is changed, and the one that records events.

```
aggregate AggregateRoot(id) extends Entity
  state: pendingEvents: list<Event>
  protected raise(event)        stamps the event with who and when, appends to pendingEvents
  operation touch(by: Actor)    updatedAt := clock.now(); updatedBy := by; re-stamps pending events
```

- Only the root is loaded, saved and referenced from outside; children are reached through it.
- Raising is protected: a service cannot raise an event for an aggregate.
- **Commonest mistake:** raising an event after the repository write; it stays on the aggregate and is never handled.

## Child entity

**Purpose:** a part that only exists inside one aggregate.

```
entity OrderLine(id, productId, quantity)
```

- No repository and no storage collection of its own; it loads whole with its root.
- Changed only by an operation of the root.
- **Commonest mistake:** a repository for a child, which lets callers change it without the root's rules.

## Value object

**Purpose:** a small group of values with no identity, compared by its parts.

```
value Address(street, city, postalCode)
```

- Immutable: a change builds a new one.
- Validates itself on construction.
- **Commonest mistake:** giving it an id; if it needs one it is an entity.

## Anti-corruption layer

**Purpose:** lets the domain ask another system a question in the domain's own words.

```
port TaxRates                                   declared in Domain
  rateFor(country) -> Percentage
adapter TaxRates over the outside tax service    implemented in Infrastructure, translates its model
```

- Declared in Domain as a port; implemented in Infrastructure, where the outside model is translated.
- Called by a domain service (or a command handler); never by an aggregate.
- **Commonest mistake:** passing the outside system's types into Domain, so its model leaks into the business code.

## Domain event

**Purpose:** a past-tense fact raised by an aggregate that other parts may react to. One operation may raise several; one event may have several handlers.

```
event Completed(orderId, customerId, by, correlationId, occurredAt)
```

- Named for what happened (`Completed`), kept in the aggregate's own events group.
- Carries ids and the context of the change, never whole aggregates.
- Its wire name is stable; renaming it breaks every consumer.
- **Commonest mistake:** an event named like a command (`CompleteOrder`) or carrying a mutable aggregate.

## Repository interface

**Purpose:** declares how the domain asks for aggregates, without saying how they are stored.

```
repository OrderRepository
  findById(id) -> option<Order>             read only
  findByIdForUpdate(id) -> option<Order>    read you intend to change
  create(order), update(order), delete(order)
  findOpenByCustomer(customerId) -> list<Order>
```

- One per aggregate, queries named for the domain's question.
- A write needs a transaction; without one it fails loudly.
- **Commonest mistake:** a generic "query by predicate" method that moves business filtering into callers.

## Domain service

**Purpose:** holds business logic that does not fit inside a single aggregate.

```
service PricingService
  priceOf(order: Order) -> Money
    products := productRepository.findByIds(order.productIds)
    tax := taxRates.rateFor(order.shippingCountry)          an anti-corruption layer
    return sum of line.quantity * products[line.productId].price, plus tax
```

- Use one when a decision needs several entities (of one or several kinds) or an outside system reached through an anti-corruption layer.
- **Default: pure.** The command handler loads the aggregates and passes them in; the service decides from what it was given and is tested without mocks.
- **Reads are allowed** when the rule itself decides what data it needs (a customer's previous orders for a loyalty rule, today's exchange rate, the promotions that apply). It reads through an interface declared in Domain, a narrow read port (`openOrderTotals.of(customerId)`) in preference to a whole repository, and only its read methods.
- **Writes are never allowed.** No create, update, upsert or delete, no save, no commit, no transaction. It changes the aggregates it was given through their operations, or returns a new aggregate or a result that says what changed; the command handler persists all of it, so one handler shows the whole persistence story of the use case.
- Stateless, named after the domain concept it serves (`PricingService`, `TransferPolicy`), never after an aggregate plus "Service" by reflex.
- Contains no rule that fits inside one aggregate; that rule is an operation of the aggregate.
- **Commonest mistakes:** a service named after an aggregate that collects every operation of it (the operations belong on the aggregate, the coordination in the command handlers); and a service that saves what it changed, so persistence is split between it and the handler.

## Command

**Purpose:** a plain request to change something, with the data the change needs.

```
command CompleteOrder(orderId) -> OrderView   [transactional, carries context]
```

- A plain record; flags say whether it needs a transaction and whether it carries context.
- Carries one `context` value (actor, correlation, causation, tenant, language), filled by the host, never by the client. A command without a context (a read) is not stamped and writes nothing.
- **Commonest mistake:** putting the acting user in the body the client sends.

## Validator

**Purpose:** checks the shape of a command before anything runs.

```
validator CompleteOrder
  require orderId is not empty
```

- Checks what can be checked from the command alone: required, format, range.
- Business rules that need data are not here; they belong to the aggregate.
- **Commonest mistake:** a validator that loads data to decide.

## Command handler

**Purpose:** runs one use case: loads what the action needs, asks the domain to decide, saves, maps the result.

```
handler CompleteOrder
  order := orders.findByIdForUpdate(command.orderId) or throw NotFound("order")
  order.complete(context.actor)
  orders.update(order)
  return OrderView.from(order)

handler TransferStock
  from := warehouses.findByIdForUpdate(command.fromId) or throw NotFound("warehouse")
  to := warehouses.findByIdForUpdate(command.toId) or throw NotFound("warehouse")
  from.release(command.productId, command.quantity, context.actor)
  to.receive(command.productId, command.quantity, context.actor)
  warehouses.update(from); warehouses.update(to)
  return TransferView.from(from, to)
```

- Also called the use case or the application service. Owns the transaction boundary through the pipeline.
- May load and change several aggregates when the action is about all of them; a consequence of the action is an event handler (the test is in the cheat sheet of [placement-guide.md](placement-guide.md)).
- Calls aggregate operations and domain services; holds no business `if` of its own.
- Never catches a business failure to hide it.
- **Commonest mistake:** a handler that grows a line every time someone wants a new side effect (count it on the customer, notify, update a report). Those are consequences: events and event handlers.

## Command pipeline

**Purpose:** the fixed sequence every command passes through.

```
pipeline(command)
  validate(command)                      failure → validation error
  before-middlewares(command)
  begin transaction if command is transactional
  result := handler.handle(command)
  commit                                 on any failure: roll back, clear events, rethrow
  after-middlewares(command, result)
  return result
```

- A command that calls another command joins the open transaction; the first to begin owns the commit.
- **Commonest mistake:** a second entry point that skips the pipeline and so skips validation.

## Dispatcher

**Purpose:** the only way to run a command: given a command, find its handler and run the pipeline.

```
dispatcher.send(command) -> result
```

- Callers name the command, never the handler.
- The same dispatcher serves requests, console runs and event handlers.
- **Commonest mistake:** a controller that calls a handler directly.

## DTO

**Purpose:** a flat, serialisable description of a result.

```
view OrderView(id, status, lines, total)
OrderView.from(order) -> OrderView          names every field it reads
```

- Built by an explicit mapping function; no reflection mapper.
- Never exposes an aggregate.
- **Commonest mistake:** returning the aggregate itself and leaking internal state.

## Sync event handler

**Purpose:** a reaction that must succeed together with the change that caused it.

```
on Completed [sync] do send CountCompletedOrder(customerId, context)
```

- Runs after the save and before the commit, inside the command's transaction.
- If it fails, the whole change is rolled back; it can veto.
- Lives in Application, sends one command.
- **Commonest mistake:** a slow call (email, network) in a sync handler.

## Async event handler

**Purpose:** a reaction that may follow the change and may fail on its own.

```
on LineAdded [async] do send ReserveStock(productId, quantity, context)
```

- Runs after the commit, in a new scope and its own transaction, as the system actor.
- If it fails, the original change stays committed.
- Must be safe to run twice: delivery is at-least-once.
- **Commonest mistake:** assuming the result of the reaction is visible to the caller's next read.

## Unit of work

**Purpose:** owns the transaction and the commit order of one scope.

```
unitOfWork
  begin(), commit(), rollback()
  commit:
    save changes
    run sync handlers (they may write and raise more events)
    COMMIT
    hand the events to the event dispatchers
    run after-commit actions
  afterCommit(action)
```

- One per scope; a scope that processes several items opens one per item.
- Implemented in Infrastructure, declared in Shared.
- **Commonest mistake:** committing inside a handler.

## Event dispatcher

**Purpose:** carries committed events to where they are handled or published.

```
eventDispatcher.dispatch(events)       implementations: in-process async handlers, log, outbox, broker
```

- Several may be registered; each receives every committed event.
- Failure of one never blocks the others.
- **Commonest mistake:** publishing before the commit; a rollback then leaves a message about something that never happened.

## Failure

**Purpose:** says what went wrong, in a stable shape every caller can branch on.

```
failure Rule(code, message)            a business rule refused; the code is a constant of the aggregate
failure NotFound(name), Duplicate(name), InvalidParameter(name, reason), ReferenceViolation(name), Forbidden(action)
failure ValidationFailed(errors: map<field, list<message>>)
anything else                          an unexpected failure: answered with a reference, never its own message
```

- Every failure has a code and a status; the host turns it into `code, message, errors`. The shape and the statuses never change with the project's level of error handling; the level only adds detail (`options/exceptions.md`).
- A rule's code is declared once, next to the aggregate, never written at the throw.
- **Commonest mistake:** returning an exception's own message for an unexpected failure, which leaks internals; and a status that changes when the level changes.

## Host adapter

**Purpose:** converts the outside world into a command with its context filled, and the outcome back.

```
host.handle(request)
  command := parse(request)
  command.context := context(actor from credential, correlation from headers, tenant, language)
  result := dispatcher.send(command)
  respond(result)  or  respond(failure.code, failure.message, failure.errors)
```

- Clears whatever the client sent in the context fields before filling them.
- Turns every failure into the same shape: code, message, errors, with the failure's status.
- **Commonest mistake:** a rule or a transaction in the adapter.
