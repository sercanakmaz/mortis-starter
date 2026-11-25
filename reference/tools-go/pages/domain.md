# Aggregates: internal/domain

One package per aggregate. An aggregate embeds `shared.AggregateRoot`, changes only through its own methods, returns an error for a broken rule, and raises an event for every change worth reacting to. It knows nothing of the database.

The example has four aggregates. `products` and `orders` are shown in full; `customers` and `categories` follow the same pattern and are shown after them.

## products

@@go internal/domain/products/product.go@@

@@go internal/domain/products/constants.go@@

@@go internal/domain/products/events.go@@

`repository.go`: a read that locks ends in `ForUpdate`; a command that changes an aggregate reads it that way, so two commands on one row run one after the other and no change is lost.

@@go internal/domain/products/repository.go@@

## orders: an aggregate with children and a value object

`Line` lives only inside its order; its unexported `cancel` is reached through `Order.CancelLine`. `Money` checks itself on creation. `Status` is a string type.

@@go internal/domain/orders/order.go@@

@@go internal/domain/orders/line.go@@

@@go internal/domain/orders/money.go@@

@@go internal/domain/orders/status.go@@

@@go internal/domain/orders/linemodel.go@@

@@go internal/domain/orders/constants.go@@

@@go internal/domain/orders/events.go@@

@@go internal/domain/orders/repository.go@@

## customers

@@go internal/domain/customers/customer.go@@

@@go internal/domain/customers/constants.go@@

@@go internal/domain/customers/events.go@@

@@go internal/domain/customers/repository.go@@

## categories: an integer key the database assigns

The repository assigns the id on insert, then calls `MarkCreated`, which raises `Created` now that the id exists.

@@go internal/domain/categories/category.go@@

@@go internal/domain/categories/constants.go@@

@@go internal/domain/categories/events.go@@

@@go internal/domain/categories/repository.go@@
