# The worked slice: tests, C#

The tests and test support of the example domain (`worked-slice.md`). Layout and rules: `testing.md`.

## Test support

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

`ProductTests`, `CustomerTests`, `OrderTests`, `MoneyTests` in `test/{Name}.Tests/Domain/{Plural}/`. Every test, in order:

@@members test/Boilerplate.Tests/Domain/Products/ProductTests.cs::ProductTests@@

@@members test/Boilerplate.Tests/Domain/Customers/CustomerTests.cs::CustomerTests@@

@@members test/Boilerplate.Tests/Domain/Orders/MoneyTests.cs::MoneyTests@@

@@members test/Boilerplate.Tests/Domain/Orders/OrderTests.cs::OrderTests@@

## Handler tests

One class per command handler that writes, in `test/{Name}.Tests/Application/{Plural}/`. The handler is built from repository builders and a `CommandPipelineBuilder`; a `UnitOfWorkBuilder` with recorded calls proves the transaction.

`CreateOrderCommandHandlerTests`:

```csharp
[Fact]
public async Task Handle_Should_ReturnTheCreatedOrderInsideATransaction()
{
    var calls = new List<string>();
    var customerId = Guid.CreateVersion7();
    var orderRepository = OrderRepositoryBuilder.Create().WithCreateAsync(_ => calls.Add(nameof(IOrderRepository.CreateAsync))).Build();
    var commandPipeline = CommandPipelineBuilder<CreateOrderCommand>.Create()
        .WithValidator(new CreateOrderCommandValidator())
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();
    var commandHandler = new CreateOrderCommandHandler(orderRepository, commandPipeline);

    var commandResult = await commandHandler.Handle(new CreateOrderCommand
    {
        OrderNumber = "ORD-1",
        CustomerId = customerId,
        Lines = [new OrderLineModel("SKU-1", 10.5m, "EUR", 2)]
    });

    Assert.Equal("ORD-1", commandResult.Content.OrderNumber);
    Assert.Equal(customerId, commandResult.Content.CustomerId);
    Assert.Equal("Open", commandResult.Content.Status);
    Assert.Equal(21m, commandResult.Content.TotalAmount);
    Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.CreateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
}
```

```csharp
[Fact]
public async Task Handle_WithoutLines_Should_ThrowValidationFailedForLinesAndNotWrite()
{
    var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithCreateAsync();
    var commandPipeline = CommandPipelineBuilder<CreateOrderCommand>.Create().WithValidator(new CreateOrderCommandValidator()).Build();
    var commandHandler = new CreateOrderCommandHandler(orderRepositoryBuilder.Build(), commandPipeline);

    var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new CreateOrderCommand
    {
        OrderNumber = "ORD-1",
        CustomerId = Guid.CreateVersion7(),
        Lines = []
    }));

    Assert.Equal([nameof(CreateOrderCommand.Lines)], exception.Errors.Keys);
    orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.CreateAsync(It.IsAny<Order>()), Times.Never);
}
```

`CompleteOrderCommandHandlerTests`, the shape of every operate-by-id handler test:

```csharp
[Fact]
public async Task Handle_Should_CompleteTheTrackedOrderAndUpdateItInsideATransaction()
{
    var calls = new List<string>();
    Order writtenOrder = null;
    var orderRepository = OrderRepositoryBuilder.Create()
        .WithFindByIdTrackingAsync(FakeOrder.Create().WithLine("SKU-1").WithoutEvents())
        .WithUpdateAsync(updatedOrder =>
        {
            writtenOrder = updatedOrder;
            calls.Add(nameof(IOrderRepository.UpdateAsync));
        })
        .Build();
    var commandPipeline = CommandPipelineBuilder<CompleteOrderCommand>.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();
    var commandHandler = new CompleteOrderCommandHandler(orderRepository, commandPipeline);

    var commandResult = await commandHandler.Handle(new CompleteOrderCommand(Guid.CreateVersion7()) { UserId = "user-1", UserEmail = "user-1@localhost" });

    Assert.Equal("Completed", commandResult.Content.Status);
    Assert.Equal("user-1", writtenOrder.UpdatedUserId);
    Assert.IsType<Completed>(Assert.Single(writtenOrder.DomainEvents));
    Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
}
```

```csharp
[Fact]
public async Task Handle_WithAnUnknownOrder_Should_ThrowNotFoundAndNotWrite()
{
    var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).WithUpdateAsync();
    var commandHandler = new CompleteOrderCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<CompleteOrderCommand>.Create().Build());

    await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new CompleteOrderCommand(Guid.CreateVersion7())));

    orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
}
```

The other handler test classes and their tests, each written in one of the two shapes above:

| Class | Tests |
|---|---|
| `CreateProductCommandHandlerTests` | `Handle_Should_ReturnTheCreatedProductInsideATransaction`, `Handle_WithNegativeStock_Should_ThrowValidationFailedForStockAndNotWrite` |
| `IncreaseProductStockCommandHandlerTests` | `Handle_Should_ReturnTheProductWithTheNewStockInsideATransaction`, `Handle_WithZeroQuantity_Should_ThrowValidationFailedForQuantityAndNotWrite` |
| `DecreaseProductStockCommandHandlerTests` | the same two names |
| `CreateCustomerCommandHandlerTests` | `Handle_Should_ReturnTheCreatedCustomerWithANormalizedEmailInsideATransaction`, `Handle_WithAnInvalidEmail_Should_ThrowValidationFailedForEmailAndNotWrite` |
| `BlockCustomerCommandHandlerTests` | `Handle_Should_BlockTheTrackedCustomerAndUpdateIt`, `Handle_WithAnUnknownCustomer_Should_ThrowNotFoundAndNotWrite` |
| `RegisterCompletedOrderCommandHandlerTests` | `Handle_Should_IncrementTheCountAndUpdateTheCustomer`, `Handle_WhenTheCustomerIsBlocked_Should_ThrowBlockedAndNotWrite` |
| `AddOrderLineCommandHandlerTests` | `Handle_Should_AddTheLineAndUpdateTheOrder`, `Handle_WhenTheOrderIsCompleted_Should_ThrowNotOpenAndNotWrite` |
| `CancelOrderLineCommandHandlerTests` | `Handle_Should_CancelTheLineAndUpdateTheOrder`, `Handle_WithAnUnknownLine_Should_ThrowNotFoundAndNotWrite` |
| `CancelOrderWhenAllLinesCancelledCommandHandlerTests` | `Handle_WithEveryLineCancelled_Should_CancelAndUpdateTheOrder`, `Handle_WithAnActiveLine_Should_ReturnTheOrderWithoutWriting` |

## Event handler tests

One class per event handler. The event handler is built with a `CommandDispatcherBuilder` holding the real command handler of the command it sends, built from repository builders:

```csharp
[Fact]
public async Task Handle_Should_DecreaseTheStockOfTheProductWithTheSkuAndQuantityOfTheEvent()
{
    Product writtenProduct = null;
    var productRepositoryBuilder = ProductRepositoryBuilder.Create()
        .WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 10))
        .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
    var commandPipeline = CommandPipelineBuilder<DecreaseProductStockCommand>.Create().WithValidator(new DecreaseProductStockCommandValidator()).Build();
    var commandHandler = new DecreaseProductStockCommandHandler(productRepositoryBuilder.Build(), commandPipeline);
    var eventHandler = new DecreaseStockWhenOrderLineAddedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

    await eventHandler.Handle(new LineAdded(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3));

    productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.FindBySkuTrackingAsync("SKU-1"), Times.Once);
    Assert.Equal(7, writtenProduct.Stock);
}
```

| Class | Tests |
|---|---|
| `DecreaseStockWhenOrderLineAddedEventHandlerTests` | the test above, `Handle_Should_RunTheCommandAsTheSystemUserWhateverUserRaisedTheEvent` |
| `IncreaseStockWhenOrderLineCancelledEventHandlerTests` | `Handle_Should_IncreaseTheStockOfTheProductWithTheSkuAndQuantityOfTheEvent`, `Handle_Should_RunTheCommandAsTheSystemUserWhateverUserRaisedTheEvent` |
| `RegisterCompletedOrderWhenOrderCompletedEventHandlerTests` | `Handle_Should_RegisterTheCompletedOrderOnTheCustomerOfTheEvent`, `Handle_Should_RunTheCommandAsTheUserWhoRaisedTheEvent` |
| `CancelOrderWhenOrderLineCancelledEventHandlerTests` | `Handle_WhenEveryLineIsCancelled_Should_CancelTheOrderOfTheEvent`, `Handle_WhenAnotherLineIsStillActive_Should_LeaveTheOrderOpenAndNotWrite`, `Handle_Should_RunTheCommandAsTheUserWhoRaisedTheEvent` |

## Persistence tests

These need no database: the context is built against a connection string that is never opened.

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Orders/OrderRepositoryTests.cs::OrderRepositoryTests@@

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Orders/OrderConfigurationTests.cs::OrderConfigurationTests@@

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductRepositoryTests.cs::ProductRepositoryTests@@

@@members test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductConfigurationTests.cs::ProductConfigurationTests@@

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.


A handler written to `application.md`'s shapes also needs `{Name}.Shared.Exceptions` (for `NotFoundException`); the handler and event handler tests need `Moq`, `{Name}.Shared.Exceptions`, `{Name}.Shared.Persistence` and the `TestSupport` namespaces of the builders they use. The build names any that is missing.

@@usings worked-slice-tests.md@@
