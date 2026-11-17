# Pseudo-code notation

Used by every file in this folder. It is typed and language-neutral: no keyword here belongs to a single programming language. It names **roles**, not identifiers; how a role is spelled is the language's naming page.

## Forms

| Form | Meaning |
|---|---|
| `aggregate X(id: XId)` | an aggregate root with its identity |
| `entity X`, `value X(a, b)` | a child entity; a value object compared by its parts |
| `state: a, b: list<T>` | the data it holds |
| `operation name(args)` | a method that changes state |
| `require condition else Rule("code")` | stop with a business-rule failure and this stable code when the condition is false |
| `raise Event(fields)` | record a domain event on the aggregate |
| `event Name(fields)` | a domain event, past tense |
| `command Name(fields) -> View [flags]` | a command and what it returns; flags: `transactional`, `carries context` |
| `handler Name` | the command handler of that command |
| `on Event [sync|async] do send Command(...)` | an event handler sending one command; one event may have several such handlers |
| `service X` / `repository X` | a domain service / a repository interface |
| `:=` | assignment |
| `list<T>`, `option<T>`, `map<K, V>` | collections |
| `return`, `if`, `for each` | as usual |

## Rules of the notation

- Types come after the name: `orderId: OrderId`.
- A failure is always `require ... else Rule(code)`, or `throw Kind(code)` for a failure that is not a business rule (not found, duplicate, forbidden).
- Time and new ids are never read directly; they come from `clock.now()` and `ids.next()`.
- `context` is the one value on every command that says who, which correlation, which tenant, which language.
- Comments are not used; the names carry the meaning.

## Example

```
aggregate Order(id: OrderId)
  state: status, lines: list<OrderLine>, customerId
  operation complete(by: Actor)
    require status == Open     else Rule("order.not_open")
    require lines is not empty else Rule("order.empty")
    status := Completed
    raise Completed(orderId: id, customerId, by)

command CompleteOrder(orderId) -> OrderView    [transactional, carries context]

handler CompleteOrder
  order := orders.findByIdForUpdate(command.orderId) or throw NotFound("order")
  order.complete(context.actor)
  orders.update(order)
  return OrderView.from(order)
```
