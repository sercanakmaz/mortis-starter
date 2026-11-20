# The worked slice: tests, C#

The tests and test support of the example domain (`worked-slice.md`). Layout and rules: `testing.md`.

## Test support

@@members test/Boilerplate.Tests/TestSupport/Domain/Categories/FakeCategory.cs::FakeCategory@@

@@members test/Boilerplate.Tests/TestSupport/Domain/Categories/CategoryRepositoryBuilder.cs::CategoryRepositoryBuilder@@

@@members test/Boilerplate.Tests/TestSupport/Domain/Products/FakeProduct.cs::FakeProduct@@

@@members test/Boilerplate.Tests/TestSupport/Domain/Customers/FakeCustomer.cs::FakeCustomer@@

@@members test/Boilerplate.Tests/TestSupport/Domain/Orders/FakeOrder.cs::FakeOrder@@

A repository builder wraps a `Mock<I{Aggregate}Repository>`, one `With...` per method a test stubs. The order's, in full:

@@decl test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::_mock@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::Create@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::WithFindByIdAsync@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::WithFindByIdTrackingAsync@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::WithCreateAsync@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::WithUpdateAsync@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::Build@@

@@member test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs::OrderRepositoryBuilder::BuildMock@@

`CustomerRepositoryBuilder` has the same members for `Customer`. `ProductRepositoryBuilder` has them for `Product` plus:

@@member test/Boilerplate.Tests/TestSupport/Domain/Products/ProductRepositoryBuilder.cs::ProductRepositoryBuilder::WithFindBySkuTrackingAsync@@

## Domain tests

`CategoryTests`, `ProductTests`, `CustomerTests`, `OrderTests`, `MoneyTests` in `test/{Name}.Tests/Domain/{Plural}/`. Every test, in order:

@@members test/Boilerplate.Tests/Domain/Categories/CategoryTests.cs::CategoryTests@@

@@members test/Boilerplate.Tests/Domain/Products/ProductTests.cs::ProductTests@@

@@members test/Boilerplate.Tests/Domain/Customers/CustomerTests.cs::CustomerTests@@

@@members test/Boilerplate.Tests/Domain/Orders/MoneyTests.cs::MoneyTests@@

@@members test/Boilerplate.Tests/Domain/Orders/OrderTests.cs::OrderTests@@

## Handler tests

One class per command handler that writes, in `test/{Name}.Tests/Application/{Plural}/`. The handler is built from repository builders and a `CommandPipelineBuilder`; a `UnitOfWorkBuilder` with recorded calls proves the transaction.

`CreateCategoryCommandHandlerTests`, the shape for an aggregate whose id the database assigns:

@@members test/Boilerplate.Tests/Application/Categories/CreateCategoryCommandHandlerTests.cs::CreateCategoryCommandHandlerTests@@

`CreateProductCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Products/CreateProductCommandHandlerTests.cs::CreateProductCommandHandlerTests@@

`IncreaseProductStockCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Products/IncreaseProductStockCommandHandlerTests.cs::IncreaseProductStockCommandHandlerTests@@

`DecreaseProductStockCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Products/DecreaseProductStockCommandHandlerTests.cs::DecreaseProductStockCommandHandlerTests@@

`CreateCustomerCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Customers/CreateCustomerCommandHandlerTests.cs::CreateCustomerCommandHandlerTests@@

`BlockCustomerCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Customers/BlockCustomerCommandHandlerTests.cs::BlockCustomerCommandHandlerTests@@

`RegisterCompletedOrderCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Customers/RegisterCompletedOrderCommandHandlerTests.cs::RegisterCompletedOrderCommandHandlerTests@@

`CreateOrderCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Orders/CreateOrderCommandHandlerTests.cs::CreateOrderCommandHandlerTests@@

`AddOrderLineCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Orders/AddOrderLineCommandHandlerTests.cs::AddOrderLineCommandHandlerTests@@

`CancelOrderLineCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Orders/CancelOrderLineCommandHandlerTests.cs::CancelOrderLineCommandHandlerTests@@

`CompleteOrderCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Orders/CompleteOrderCommandHandlerTests.cs::CompleteOrderCommandHandlerTests@@

`CancelOrderWhenAllLinesCancelledCommandHandlerTests`:

@@members test/Boilerplate.Tests/Application/Orders/CancelOrderWhenAllLinesCancelledCommandHandlerTests.cs::CancelOrderWhenAllLinesCancelledCommandHandlerTests@@

## Event handler tests

One class per event handler. The event handler is built with a `CommandDispatcherBuilder` holding the real command handler of the command it sends, built from repository builders.

`DecreaseStockWhenOrderLineAddedEventHandlerTests`:

@@members test/Boilerplate.Tests/Application/Products/DecreaseStockWhenOrderLineAddedEventHandlerTests.cs::DecreaseStockWhenOrderLineAddedEventHandlerTests@@

`IncreaseStockWhenOrderLineCancelledEventHandlerTests`:

@@members test/Boilerplate.Tests/Application/Products/IncreaseStockWhenOrderLineCancelledEventHandlerTests.cs::IncreaseStockWhenOrderLineCancelledEventHandlerTests@@

`RegisterCompletedOrderWhenOrderCompletedEventHandlerTests`:

@@members test/Boilerplate.Tests/Application/Customers/RegisterCompletedOrderWhenOrderCompletedEventHandlerTests.cs::RegisterCompletedOrderWhenOrderCompletedEventHandlerTests@@

`CancelOrderWhenOrderLineCancelledEventHandlerTests`:

@@members test/Boilerplate.Tests/Application/Orders/CancelOrderWhenOrderLineCancelledEventHandlerTests.cs::CancelOrderWhenOrderLineCancelledEventHandlerTests@@

## Persistence tests

These need no database: the context is built against a connection string that is never opened.

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Orders/OrderRepositoryTests.cs::OrderRepositoryTests@@

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Orders/OrderConfigurationTests.cs::OrderConfigurationTests@@

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductRepositoryTests.cs::ProductRepositoryTests@@

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductConfigurationTests.cs::ProductConfigurationTests@@


@@members test/Boilerplate.Tests/Infrastructure/Persistence/Categories/CategoryConfigurationTests.cs::CategoryConfigurationTests@@

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.


A handler written to `application.md`'s shapes also needs `{Name}.Shared.Exceptions` (for `NotFoundException`); the handler and event handler tests need `Moq`, `{Name}.Shared.Exceptions`, `{Name}.Shared.Persistence` and the `TestSupport` namespaces of the builders they use. The build names any that is missing.

@@usings worked-slice-tests.md@@
