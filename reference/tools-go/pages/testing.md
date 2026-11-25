# Tests

The `testing` package, table tests where a unit has several cases, and no mocking library: `internal/testsupport` has an in-memory unit of work and repositories that behave like the real ones (events raised on write, sync handlers before the end, consumers' events recorded). Unit tests need no database. Test files sit beside the code, in the external test package (`products_test`) unless they test unexported code.

@@go internal/testsupport/unit_of_work.go@@

With the example:

@@go internal/testsupport/repositories.go@@

## The kernel's tests

@@go internal/shared/dispatcher_test.go@@

@@go internal/shared/eventcontext_test.go@@

## The example's tests

@@go internal/domain/products/product_test.go@@

@@go internal/domain/orders/order_test.go@@

@@go internal/domain/customers/customer_test.go@@

`internal/application/flow_test.go` runs the real handlers and sync handlers through the dispatcher over the in-memory doubles (`Categories` is left nil: no flow touches a category):

@@go internal/application/flow_test.go@@

## The hosts' tests

@@go cmd/console/arguments_test.go@@

@@go internal/api/respond_test.go@@

@@go internal/infrastructure/persistence/connection_string_test.go@@

The database behaviour (locks, foreign keys, the publisher after commit) is proved by `verify.sh`'s conformance scripts against a real PostgreSQL.
