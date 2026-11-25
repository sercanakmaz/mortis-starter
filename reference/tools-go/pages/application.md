# Commands and handlers: internal/application

One package per aggregate, importing its own domain package as `domain`. A command is a struct embedding `shared.WithContext`, and `shared.InTransaction` when it writes; it validates its own shape in `Validate`. Its handler is a struct holding the repositories it needs, with one method `Handle(ctx, *Command) (Result, error)`. A command that reads (`Get...Command`) has no transaction. A missing id in a create command gets `shared.NewID()` in the handler.

## products, in full

@@go internal/application/products/dto.go@@

@@go internal/application/products/create_product.go@@

@@go internal/application/products/get_product.go@@

@@go internal/application/products/decrease_product_stock.go@@

`increase_product_stock.go` is the same with `Increase` for `Decrease`.

A consumer reacts after the order committed, as the user whose command raised the event:

@@go internal/application/products/decrease_stock_when_order_line_added.go@@

`increase_stock_when_order_line_cancelled.go` is the same over `orders.LineCancelled`, sending `IncreaseProductStockCommand`.

## orders

@@go internal/application/orders/dto.go@@

@@go internal/application/orders/line_rules.go@@

@@go internal/application/orders/load.go@@

@@go internal/application/orders/create_order.go@@

@@go internal/application/orders/get_order.go@@

@@go internal/application/orders/add_order_line.go@@

@@go internal/application/orders/cancel_order_line.go@@

@@go internal/application/orders/complete_order.go@@

@@go internal/application/orders/cancel_order_when_all_lines_cancelled.go@@

A sync handler runs in the transaction of the command that raised the event; its failure rolls that command back:

@@go internal/application/orders/cancel_order_when_order_line_cancelled.go@@

## customers

@@go internal/application/customers/dto.go@@

@@go internal/application/customers/create_customer.go@@

@@go internal/application/customers/get_customer.go@@

@@go internal/application/customers/block_customer.go@@

@@go internal/application/customers/register_completed_order.go@@

@@go internal/application/customers/register_completed_order_when_order_completed.go@@

## categories

@@go internal/application/categories/dto.go@@

@@go internal/application/categories/create_category.go@@

@@go internal/application/categories/get_category.go@@

## Registration

`internal/application/module.go` registers every handler, sync handler and consumer. Without the example, `Repositories` is an empty struct and `Register` registers nothing yet.

@@go internal/application/module.go@@
