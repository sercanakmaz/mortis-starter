# The worked slice: tests, C#

The tests and test support of the example domain (`worked-slice.md`). Layout and rules: `testing.md`.

## Test support

```csharp
public class FakeProduct : Product
```

```csharp
private FakeProduct(Guid id, string sku, int stock) : base(id, sku, "Default Product", stock, FakeAuditUser.Create())
{
}
```

```csharp
private FakeProduct() { }
```

```csharp
public static FakeProduct Create(Guid id, string sku, int stock) => new(id, sku, stock);
```

```csharp
public static FakeProduct Create(string sku = "SKU-1", int stock = 10) => Create(Guid.CreateVersion7(), sku, stock);
```

```csharp
public static FakeProduct CreateEmpty() => new();
```

```csharp
public FakeProduct WithoutEvents()
{
    ClearEvents();
    return this;
}
```

```csharp
public class FakeCustomer : Customer
```

```csharp
private FakeCustomer(Guid id) : base(id, "Default Customer", "default-customer@example.com", FakeAuditUser.Create())
{
}
```

```csharp
private FakeCustomer() { }
```

```csharp
public static FakeCustomer Create(Guid id) => new(id);
```

```csharp
public static FakeCustomer Create() => Create(Guid.CreateVersion7());
```

```csharp
public static FakeCustomer CreateEmpty() => new();
```

```csharp
public FakeCustomer WithName(string name)
{
    Name = name;
    return this;
}
```

```csharp
public FakeCustomer WithEmail(string email)
{
    Email = email;
    return this;
}
```

```csharp
public FakeCustomer WithIsBlocked(bool isBlocked)
{
    IsBlocked = isBlocked;
    return this;
}
```

```csharp
public FakeCustomer WithCompletedOrderCount(int completedOrderCount)
{
    CompletedOrderCount = completedOrderCount;
    return this;
}
```

```csharp
public FakeCustomer WithoutEvents()
{
    ClearEvents();
    return this;
}
```

```csharp
public class FakeOrder : Order
```

```csharp
private FakeOrder(Guid id, Guid customerId) : base(id, "ORD-DEFAULT", customerId, FakeAuditUser.Create())
{
}
```

```csharp
private FakeOrder() { }
```

```csharp
public static FakeOrder Create(Guid id) => new(id, Guid.CreateVersion7());
```

```csharp
public static FakeOrder Create() => Create(Guid.CreateVersion7());
```

```csharp
public static FakeOrder CreateEmpty() => new();
```

```csharp
public FakeOrder WithOrderNumber(string orderNumber)
{
    OrderNumber = orderNumber;
    return this;
}
```

```csharp
public FakeOrder WithCustomerId(Guid customerId)
{
    CustomerId = customerId;
    return this;
}
```

```csharp
public FakeOrder WithLine(string sku, decimal price = 10m, string currency = "EUR", int quantity = 1)
{
    AddLine(sku, new Money(price, currency), quantity);
    return this;
}
```

```csharp
public FakeOrder WithCompleted()
{
    Complete();
    return this;
}
```

```csharp
public FakeOrder WithCancelledLine(string sku)
{
    CancelLine(Lines.First(orderLine => orderLine.Sku == sku).Id, "test");
    return this;
}
```

```csharp
public FakeOrder WithCancelled()
{
    foreach (var orderLine in Lines)
    {
        CancelLine(orderLine.Id, "test");
    }

    CancelWhenAllLinesCancelled();
    return this;
}
```

```csharp
public FakeOrder WithoutEvents()
{
    ClearEvents();
    return this;
}
```

A repository builder wraps a `Mock<I{Aggregate}Repository>`, one `With...` per method a test stubs. The order's, in full:

```csharp
public class OrderRepositoryBuilder
```

```csharp
private readonly Mock<IOrderRepository> _mock = new();
```

```csharp
public static OrderRepositoryBuilder Create() => new();
```

```csharp
public OrderRepositoryBuilder WithFindByIdAsync(Order order)
{
    _mock.Setup(orderRepository => orderRepository.FindByIdAsync(It.IsAny<Guid>())).ReturnsAsync(order);
    return this;
}
```

```csharp
public OrderRepositoryBuilder WithFindByIdTrackingAsync(Order order)
{
    _mock.Setup(orderRepository => orderRepository.FindByIdTrackingAsync(It.IsAny<Guid>())).ReturnsAsync(order);
    return this;
}
```

```csharp
public OrderRepositoryBuilder WithCreateAsync(Action<Order> callback = null)
{
    _mock.Setup(orderRepository => orderRepository.CreateAsync(It.IsAny<Order>()))
        .Callback<Order>(order => callback?.Invoke(order))
        .Returns(Task.CompletedTask);
    return this;
}
```

```csharp
public OrderRepositoryBuilder WithUpdateAsync(Action<Order> callback = null)
{
    _mock.Setup(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()))
        .Callback<Order>(order => callback?.Invoke(order))
        .Returns(Task.CompletedTask);
    return this;
}
```

```csharp
public IOrderRepository Build() => _mock.Object;
```

```csharp
public Mock<IOrderRepository> BuildMock() => _mock;
```

`CustomerRepositoryBuilder` has the same members for `Customer`. `ProductRepositoryBuilder` has them for `Product` plus:

```csharp
public ProductRepositoryBuilder WithFindBySkuTrackingAsync(Product product)
{
    _mock.Setup(productRepository => productRepository.FindBySkuTrackingAsync(It.IsAny<string>())).ReturnsAsync(product);
    return this;
}
```

## Domain tests

`ProductTests`, `CustomerTests`, `OrderTests`, `MoneyTests` in `test/{Name}.Tests/Domain/{Plural}/`. Every test, in order:

```csharp
public class ProductTests
```

```csharp
[Fact]
public void Empty_Constructor_Should_Create()
{
    Assert.NotNull(FakeProduct.CreateEmpty());
}
```

```csharp
[Fact]
public void Constructor_Should_SetTheStockAndRaiseCreated()
{
    var id = Guid.CreateVersion7();

    var product = new Product(id, "SKU-1", "Bolt", 10, FakeAuditUser.Create());

    Assert.Equal(10, product.Stock);
    var created = Assert.IsType<Created>(Assert.Single(product.DomainEvents));
    Assert.Equal(id.ToString(), created.Id);
    Assert.Equal("SKU-1", created.Sku);
}
```

```csharp
[Fact]
public void Constructor_WithNegativeStock_Should_ThrowInvalidParameterForStock()
{
    var exception = Assert.Throws<InvalidParameterException>(() => new Product(Guid.CreateVersion7(), "SKU-1", "Bolt", -1, FakeAuditUser.Create()));

    Assert.StartsWith("stock is invalid", exception.Message);
}
```

```csharp
[Fact]
public void IncreaseStock_Should_AddTheQuantityAndRaiseStockIncreased()
{
    var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();

    product.IncreaseStock(3);

    Assert.Equal(13, product.Stock);
    var stockIncreased = Assert.IsType<StockIncreased>(Assert.Single(product.DomainEvents));
    Assert.Equal(product.Id.ToString(), stockIncreased.Id);
    Assert.Equal("SKU-1", stockIncreased.Sku);
    Assert.Equal(3, stockIncreased.Quantity);
    Assert.Equal(13, stockIncreased.Stock);
}
```

```csharp
[Fact]
public void IncreaseStock_WithZeroQuantity_Should_ThrowInvalidParameterAndKeepTheStock()
{
    var product = FakeProduct.Create("SKU-1", 10);

    var exception = Assert.Throws<InvalidParameterException>(() => product.IncreaseStock(0));

    Assert.StartsWith("quantity is invalid", exception.Message);
    Assert.Equal(10, product.Stock);
}
```

```csharp
[Fact]
public void DecreaseStock_Should_SubtractTheQuantityAndRaiseStockDecreased()
{
    var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();

    product.DecreaseStock(4);

    Assert.Equal(6, product.Stock);
    var stockDecreased = Assert.IsType<StockDecreased>(Assert.Single(product.DomainEvents));
    Assert.Equal(4, stockDecreased.Quantity);
    Assert.Equal(6, stockDecreased.Stock);
}
```

```csharp
[Fact]
public void DecreaseStock_WithTheWholeStock_Should_LeaveZero()
{
    var product = FakeProduct.Create("SKU-1", 10);

    product.DecreaseStock(10);

    Assert.Equal(0, product.Stock);
}
```

```csharp
[Fact]
public void DecreaseStock_WithMoreThanTheStock_Should_ThrowInsufficientStockAndKeepTheStock()
{
    var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();

    var exception = Assert.Throws<BusinessRuleException>(() => product.DecreaseStock(11));

    Assert.Equal(ProductConstants.InsufficientStockCode, exception.Code);
    Assert.Equal(10, product.Stock);
    Assert.Null(product.DomainEvents?.FirstOrDefault());
}
```

```csharp
[Fact]
public void DecreaseStock_WithZeroQuantity_Should_ThrowInvalidParameter()
{
    var product = FakeProduct.Create("SKU-1", 10);

    var exception = Assert.Throws<InvalidParameterException>(() => product.DecreaseStock(0));

    Assert.StartsWith("quantity is invalid", exception.Message);
}
```

```csharp
public class CustomerTests
```

```csharp
[Fact]
public void Empty_Constructor_Should_Create()
{
    Assert.NotNull(FakeCustomer.CreateEmpty());
}
```

```csharp
[Fact]
public void Constructor_Should_NormalizeTheEmailAndRaiseCreated()
{
    var id = Guid.CreateVersion7();

    var customer = new Customer(id, "Ada", "  Ada@Example.COM ", FakeAuditUser.Create());

    Assert.Equal("ada@example.com", customer.Email);
    var created = Assert.IsType<Created>(Assert.Single(customer.DomainEvents));
    Assert.Equal(id.ToString(), created.Id);
    Assert.Equal("ada@example.com", created.Email);
}
```

```csharp
[Fact]
public void Block_Should_BlockTheCustomerAndRaiseBlocked()
{
    var customer = FakeCustomer.Create().WithoutEvents();

    customer.Block();

    Assert.True(customer.IsBlocked);
    Assert.IsType<Blocked>(Assert.Single(customer.DomainEvents));
}
```

```csharp
[Fact]
public void RegisterCompletedOrder_Should_IncrementTheCount()
{
    var customer = FakeCustomer.Create().WithCompletedOrderCount(2);

    customer.RegisterCompletedOrder();

    Assert.Equal(3, customer.CompletedOrderCount);
}
```

```csharp
[Fact]
public void RegisterCompletedOrder_WhenBlocked_Should_ThrowBlockedAndKeepTheCount()
{
    var customer = FakeCustomer.Create().WithCompletedOrderCount(2).WithIsBlocked(true);

    var exception = Assert.Throws<BusinessRuleException>(() => customer.RegisterCompletedOrder());

    Assert.Equal(CustomerConstants.BlockedCode, exception.Code);
    Assert.Equal(2, customer.CompletedOrderCount);
}
```

```csharp
public class MoneyTests
```

```csharp
[Fact]
public void Constructor_Should_TrimAndUpperCaseTheCurrency()
{
    var money = new Money(12.5m, " eur ");

    Assert.Equal(12.5m, money.Amount);
    Assert.Equal("EUR", money.Currency);
}
```

```csharp
[Fact]
public void Constructor_UnderTurkishCulture_Should_UpperCaseTheCurrencyInvariantly()
{
    var previousCulture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

    var money = new Money(1m, "inr");

    CultureInfo.CurrentCulture = previousCulture;
    Assert.Equal("INR", money.Currency);
}
```

```csharp
[Fact]
public void Constructor_WithNegativeAmount_Should_ThrowInvalidParameterForAmount()
{
    var exception = Assert.Throws<InvalidParameterException>(() => new Money(-0.01m, "EUR"));

    Assert.StartsWith("amount is invalid", exception.Message);
}
```

```csharp
[Fact]
public void Constructor_WithTwoLetterCurrency_Should_ThrowInvalidParameterForCurrency()
{
    var exception = Assert.Throws<InvalidParameterException>(() => new Money(1m, "EU"));

    Assert.StartsWith("currency is invalid", exception.Message);
}
```

```csharp
[Fact]
public void Equals_WithSameAmountAndCurrency_Should_BeEqual()
{
    Assert.Equal(new Money(5m, "eur"), new Money(5m, "EUR"));
    Assert.NotEqual(new Money(5m, "EUR"), new Money(5m, "USD"));
}
```

```csharp
public class OrderTests
```

```csharp
[Fact]
public void Empty_Constructor_Should_Create()
{
    var order = FakeOrder.CreateEmpty();

    Assert.NotNull(order);
    Assert.Empty(order.Lines);
}
```

```csharp
[Fact]
public void Constructor_Should_OpenTheOrderAndRaiseCreated()
{
    var id = Guid.CreateVersion7();
    var customerId = Guid.CreateVersion7();

    var order = FakeOrder.Create(id).WithCustomerId(customerId);

    var created = Assert.IsType<Created>(Assert.Single(order.DomainEvents));
    Assert.Equal(id.ToString(), created.Id);
    Assert.Equal("ORD-DEFAULT", created.OrderNumber);
    Assert.Equal(OrderStatus.Open, order.Status);
}
```

```csharp
[Fact]
public void AddLine_Should_AddALineThatBelongsToTheOrder()
{
    var order = FakeOrder.Create();

    var orderLine = order.AddLine("SKU-1", new Money(10.5m, "EUR"), 2);

    Assert.Same(orderLine, Assert.Single(order.Lines));
    Assert.Equal(order.Id, orderLine.OrderId);
    Assert.NotEqual(Guid.Empty, orderLine.Id);
    Assert.Equal(21m, order.TotalAmount);
}
```

```csharp
[Fact]
public void AddLine_Should_RaiseLineAddedWithTheSkuAndTheQuantity()
{
    var order = FakeOrder.Create().WithoutEvents();

    var orderLine = order.AddLine("SKU-1", new Money(10.5m, "EUR"), 3);

    var lineAdded = Assert.IsType<LineAdded>(Assert.Single(order.DomainEvents));
    Assert.Equal(order.Id.ToString(), lineAdded.Id);
    Assert.Equal(orderLine.Id.ToString(), lineAdded.LineId);
    Assert.Equal("SKU-1", lineAdded.Sku);
    Assert.Equal(3, lineAdded.Quantity);
}
```

```csharp
[Fact]
public void AddLine_WithSkuOfAnActiveLine_Should_ThrowDuplicate()
{
    var order = FakeOrder.Create().WithLine("SKU-1");

    var exception = Assert.Throws<DuplicateException>(() => order.AddLine("SKU-1", new Money(1m, "EUR"), 1));

    Assert.Equal("OrderLine already exists.", exception.Message);
    Assert.Single(order.Lines);
}
```

```csharp
[Fact]
public void AddLine_WithAnotherCurrency_Should_ThrowMixedCurrency()
{
    var order = FakeOrder.Create().WithLine("SKU-1", currency: "EUR");

    var exception = Assert.Throws<BusinessRuleException>(() => order.AddLine("SKU-2", new Money(1m, "USD"), 1));

    Assert.Equal(OrderConstants.MixedCurrencyCode, exception.Code);
}
```

```csharp
[Fact]
public void AddLine_WithZeroQuantity_Should_ThrowInvalidParameterForQuantity()
{
    var order = FakeOrder.Create();

    var exception = Assert.Throws<InvalidParameterException>(() => order.AddLine("SKU-1", new Money(1m, "EUR"), 0));

    Assert.StartsWith("quantity is invalid", exception.Message);
}
```

```csharp
[Fact]
public void AddLine_WhenTheOrderIsCompleted_Should_ThrowNotOpen()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithCompleted();

    var exception = Assert.Throws<BusinessRuleException>(() => order.AddLine("SKU-2", new Money(1m, "EUR"), 1));

    Assert.Equal(OrderConstants.NotOpenCode, exception.Code);
}
```

```csharp
[Fact]
public void CancelLine_WithAnotherActiveLine_Should_KeepTheOrderOpenAndDropTheLineFromTheTotal()
{
    var order = FakeOrder.Create().WithLine("SKU-1", price: 10m).WithLine("SKU-2", price: 4m).WithoutEvents();
    var cancelledLine = order.Lines.First(orderLine => orderLine.Sku == "SKU-1");

    order.CancelLine(cancelledLine.Id, "out of stock");

    Assert.True(cancelledLine.IsCancelled);
    Assert.Equal("out of stock", cancelledLine.CancellationReason);
    Assert.Equal(4m, order.TotalAmount);
    Assert.Equal(OrderStatus.Open, order.Status);
    Assert.IsType<LineCancelled>(Assert.Single(order.DomainEvents));
}
```

```csharp
[Fact]
public void CancelLine_Should_RaiseLineCancelledWithTheSkuAndTheQuantityOfTheLine()
{
    var order = FakeOrder.Create().WithLine("SKU-1", quantity: 4).WithoutEvents();
    var orderLine = order.Lines.First();

    order.CancelLine(orderLine.Id, "out of stock");

    var lineCancelled = Assert.IsType<LineCancelled>(Assert.Single(order.DomainEvents));
    Assert.Equal(order.Id.ToString(), lineCancelled.Id);
    Assert.Equal(orderLine.Id.ToString(), lineCancelled.LineId);
    Assert.Equal("SKU-1", lineCancelled.Sku);
    Assert.Equal(4, lineCancelled.Quantity);
    Assert.Equal("out of stock", lineCancelled.Reason);
}
```

```csharp
[Fact]
public void CancelLine_WithTheLastActiveLine_Should_LeaveCancellingTheOrderToTheEventHandler()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithoutEvents();

    order.CancelLine(order.Lines.First().Id, "out of stock");

    Assert.Equal(OrderStatus.Open, order.Status);
    Assert.IsType<LineCancelled>(Assert.Single(order.DomainEvents));
}
```

```csharp
[Fact]
public void CancelWhenAllLinesCancelled_WithEveryLineCancelled_Should_CancelTheOrderAndRaiseCancelled()
{
    var customerId = Guid.CreateVersion7();
    var order = FakeOrder.Create().WithCustomerId(customerId).WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithCancelledLine("SKU-2").WithoutEvents();

    var isCancelled = order.CancelWhenAllLinesCancelled();

    Assert.True(isCancelled);
    Assert.Equal(OrderStatus.Cancelled, order.Status);
    var cancelled = Assert.IsType<Cancelled>(Assert.Single(order.DomainEvents));
    Assert.Equal(order.Id.ToString(), cancelled.Id);
    Assert.Equal(customerId.ToString(), cancelled.CustomerId);
}
```

```csharp
[Fact]
public void CancelWhenAllLinesCancelled_WithAnActiveLine_Should_KeepTheOrderOpenAndRaiseNothing()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithoutEvents();

    var isCancelled = order.CancelWhenAllLinesCancelled();

    Assert.False(isCancelled);
    Assert.Equal(OrderStatus.Open, order.Status);
    Assert.Null(order.DomainEvents?.FirstOrDefault());
}
```

```csharp
[Fact]
public void CancelWhenAllLinesCancelled_WhenAlreadyCancelled_Should_DoNothingTheSecondTime()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithCancelled().WithoutEvents();

    var isCancelled = order.CancelWhenAllLinesCancelled();

    Assert.False(isCancelled);
    Assert.Equal(OrderStatus.Cancelled, order.Status);
    Assert.Null(order.DomainEvents?.FirstOrDefault());
}
```

```csharp
[Fact]
public void CancelWhenAllLinesCancelled_WhenTheOrderIsCompleted_Should_NotCancelIt()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithCompleted().WithoutEvents();

    var isCancelled = order.CancelWhenAllLinesCancelled();

    Assert.False(isCancelled);
    Assert.Equal(OrderStatus.Completed, order.Status);
}
```

```csharp
[Fact]
public void CancelLine_WithUnknownLine_Should_ThrowNotFoundForOrderLine()
{
    var order = FakeOrder.Create().WithLine("SKU-1");

    var exception = Assert.Throws<NotFoundException>(() => order.CancelLine(Guid.CreateVersion7(), "out of stock"));

    Assert.Equal("OrderLine was not found.", exception.Message);
}
```

```csharp
[Fact]
public void Complete_Should_RaiseCompletedWithTheCustomer()
{
    var customerId = Guid.CreateVersion7();
    var order = FakeOrder.Create().WithCustomerId(customerId).WithLine("SKU-1").WithoutEvents();

    order.Complete();

    var completed = Assert.IsType<Completed>(Assert.Single(order.DomainEvents));
    Assert.Equal(order.Id.ToString(), completed.Id);
    Assert.Equal(customerId.ToString(), completed.CustomerId);
    Assert.Equal(OrderStatus.Completed, order.Status);
}
```

```csharp
[Fact]
public void Complete_AsFulfilledWithoutLines_Should_ThrowNothingToFulfil()
{
    var order = FakeOrder.Create();

    var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

    Assert.Equal(OrderConstants.NothingToFulfilCode, exception.Code);
    Assert.Equal(OrderStatus.Open, order.Status);
}
```

```csharp
[Fact]
public void Complete_WithEveryLineCancelled_Should_ThrowNothingToFulfil()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1");

    var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

    Assert.Equal(OrderConstants.NothingToFulfilCode, exception.Code);
}
```

```csharp
[Fact]
public void Complete_WhenTheOrderIsCancelled_Should_ThrowNotOpen()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithCancelled();

    var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

    Assert.Equal(OrderConstants.NotOpenCode, exception.Code);
}
```

```csharp
[Fact]
public void Complete_WhenAlreadyCompleted_Should_ThrowNotOpen()
{
    var order = FakeOrder.Create().WithLine("SKU-1").WithCompleted();

    var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

    Assert.Equal(OrderConstants.NotOpenCode, exception.Code);
}
```

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

    var commandResult = await commandHandler.Handle(new CompleteOrderCommand(Guid.CreateVersion7()) { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

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

```csharp
public class OrderRepositoryTests
```

```csharp
[Fact]
public async Task CreateAsync_Should_HandTheRaisedEventsToTheEventContextBeforeSaving()
{
    var raisedCountAtSave = -1;
    var eventContext = new EventContext();
    var order = FakeOrder.Create();
    var unitOfWork = UnitOfWorkBuilder.Create()
        .WithHasTransaction(true)
        .WithSaveChangesAsync(() => raisedCountAtSave = eventContext.RaisedCount)
        .Build();
    var orderRepository = new OrderRepository(AppDbContextBuilder.Create().Build(), unitOfWork, eventContext);

    await orderRepository.CreateAsync(order);

    Assert.Equal(1, raisedCountAtSave);
    Assert.IsType<Created>(eventContext.TakeRaised());
    Assert.Empty(order.DomainEvents);
}
```

```csharp
[Fact]
public async Task CreateAsync_WithoutATransaction_Should_ThrowTransactionRequiredAndNotSave()
{
    var unitOfWorkBuilder = UnitOfWorkBuilder.Create().WithHasTransaction(false).WithSaveChangesAsync();
    var orderRepository = new OrderRepository(AppDbContextBuilder.Create().Build(), unitOfWorkBuilder.Build(), new EventContext());

    var exception = await Assert.ThrowsAsync<TransactionRequiredException>(() => orderRepository.CreateAsync(FakeOrder.Create()));

    Assert.Contains(nameof(Order), exception.Message);
    unitOfWorkBuilder.BuildMock().Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Never);
}
```

```csharp
[Fact]
public async Task UpdateAsync_WithAnOrderThatWasNotLoadedForTracking_Should_ThrowAndKeepItsEvents()
{
    var eventContext = new EventContext();
    var order = FakeOrder.Create();
    var unitOfWork = UnitOfWorkBuilder.Create().WithHasTransaction(true).WithSaveChangesAsync().Build();
    var orderRepository = new OrderRepository(AppDbContextBuilder.Create().Build(), unitOfWork, eventContext);

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => orderRepository.UpdateAsync(order));

    Assert.Contains("FindByIdTrackingAsync", exception.Message);
    Assert.Equal(0, eventContext.RaisedCount);
    Assert.Single(order.DomainEvents);
}
```

```csharp
[Fact]
public async Task UpdateAsync_WithALineAddedToATrackedOrder_Should_InsertTheLineInsteadOfUpdatingIt()
{
    var stateOfNewLineAtSave = EntityState.Detached;
    var context = AppDbContextBuilder.Create().Build();
    var order = FakeOrder.Create().WithLine("SKU-1").WithoutEvents();
    context.Attach(order);
    var newLine = order.AddLine("SKU-2", new Money(2m, "EUR"), 1);
    var unitOfWork = UnitOfWorkBuilder.Create()
        .WithHasTransaction(true)
        .WithSaveChangesAsync(() =>
        {
            context.ChangeTracker.DetectChanges();
            stateOfNewLineAtSave = context.Entry(newLine).State;
        })
        .Build();
    var orderRepository = new OrderRepository(context, unitOfWork, new EventContext());

    await orderRepository.UpdateAsync(order);

    Assert.Equal(EntityState.Added, stateOfNewLineAtSave);
    Assert.Equal(EntityState.Unchanged, context.Entry(order.Lines.First(orderLine => orderLine.Sku == "SKU-1")).State);
}
```

```csharp
[Fact]
public async Task UpdateAsync_WithACompletedTrackedOrder_Should_HandCompletedToTheEventContext()
{
    var eventContext = new EventContext();
    var context = AppDbContextBuilder.Create().Build();
    var order = FakeOrder.Create().WithLine("SKU-1").WithoutEvents();
    context.Attach(order);
    order.Complete();
    var unitOfWork = UnitOfWorkBuilder.Create().WithHasTransaction(true).WithSaveChangesAsync().Build();
    var orderRepository = new OrderRepository(context, unitOfWork, eventContext);

    await orderRepository.UpdateAsync(order);

    Assert.IsType<Completed>(eventContext.TakeRaised());
    Assert.Empty(order.DomainEvents);
}
```

```csharp
public class OrderConfigurationTests
```

```csharp
[Fact]
public void Model_Should_MapOrdersAndTheirLinesToSnakeCaseTables()
{
    var model = AppDbContextBuilder.Create().Build().Model;

    Assert.Equal("orders", model.FindEntityType(typeof(Order)).GetTableName());
    Assert.Equal("order_lines", model.FindEntityType(typeof(OrderLine)).GetTableName());
    Assert.Equal("order_number", model.FindEntityType(typeof(Order)).FindProperty(nameof(Order.OrderNumber)).GetColumnName());
}
```

```csharp
[Fact]
public void Model_Should_ReferenceTheCustomerThroughARestrictingForeignKey()
{
    var model = AppDbContextBuilder.Create().Build().Model;

    var foreignKey = Assert.Single(model.FindEntityType(typeof(Order)).GetForeignKeys());

    Assert.Equal(typeof(Customer), foreignKey.PrincipalEntityType.ClrType);
    Assert.Equal(nameof(Order.CustomerId), Assert.Single(foreignKey.Properties).Name);
    Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
}
```

```csharp
[Fact]
public void Model_Should_NeverLetTheDatabaseGenerateIds()
{
    var model = AppDbContextBuilder.Create().Build().Model;

    Assert.Equal(ValueGenerated.Never, model.FindEntityType(typeof(Order)).FindProperty(nameof(Order.Id)).ValueGenerated);
    Assert.Equal(ValueGenerated.Never, model.FindEntityType(typeof(OrderLine)).FindProperty(nameof(OrderLine.Id)).ValueGenerated);
}
```

```csharp
[Fact]
public void Model_Should_NotMapDomainEventsOrTheVersion()
{
    var orderEntityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Order));

    Assert.Null(orderEntityType.FindProperty(nameof(Order.Version)));
    Assert.Null(orderEntityType.FindNavigation(nameof(Order.DomainEvents)));
    Assert.Null(orderEntityType.FindProperty(nameof(Order.TotalAmount)));
}
```

```csharp
public class ProductRepositoryTests
```

```csharp
[Fact]
public async Task UpdateAsync_WithATrackedProductWhoseStockChanged_Should_HandStockDecreasedToTheEventContext()
{
    var eventContext = new EventContext();
    var context = AppDbContextBuilder.Create().Build();
    var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();
    context.Attach(product);
    product.DecreaseStock(2);
    var unitOfWork = UnitOfWorkBuilder.Create().WithHasTransaction(true).WithSaveChangesAsync().Build();
    var productRepository = new ProductRepository(context, unitOfWork, eventContext);

    await productRepository.UpdateAsync(product);

    var stockDecreased = Assert.IsType<StockDecreased>(eventContext.TakeRaised());
    Assert.Equal(8, stockDecreased.Stock);
    Assert.Empty(product.DomainEvents);
}
```

```csharp
public class ProductConfigurationTests
```

```csharp
[Fact]
public void Model_Should_MapProductsToASnakeCaseTable()
{
    var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product));

    Assert.Equal("products", entityType.GetTableName());
    Assert.Equal("stock", entityType.FindProperty(nameof(Product.Stock)).GetColumnName());
}
```

```csharp
[Fact]
public void Model_Should_MakeTheSkuAnAlternateKeySoOrderLinesCanReferenceIt()
{
    var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product));

    var key = Assert.Single(entityType.GetKeys(), candidate => !candidate.IsPrimaryKey());

    Assert.Equal(nameof(Product.Sku), Assert.Single(key.Properties).Name);
}
```

```csharp
[Fact]
public void Model_Should_GuardTheStockWithARowVersionAndACheckConstraint()
{
    var entityType = AppDbContextBuilder.Create().Build().GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Product));

    Assert.True(entityType.FindProperty(PersistenceConstants.RowVersionColumn).IsConcurrencyToken);
    Assert.Equal("stock >= 0", Assert.Single(entityType.GetCheckConstraints()).Sql);
}
```

```csharp
[Fact]
public void Model_Should_LetOrderLinesReferenceTheProductSkuThroughARestrictingForeignKey()
{
    var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(OrderLine));

    var foreignKey = Assert.Single(entityType.GetForeignKeys(), candidate => candidate.PrincipalEntityType.ClrType == typeof(Product));

    Assert.Equal(nameof(OrderLine.Sku), Assert.Single(foreignKey.Properties).Name);
    Assert.Equal(nameof(Product.Sku), Assert.Single(foreignKey.PrincipalKey.Properties).Name);
    Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
}
```

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.


A handler written to `application.md`'s shapes also needs `{Name}.Shared.Exceptions` (for `NotFoundException`); the handler and event handler tests need `Moq`, `{Name}.Shared.Exceptions`, `{Name}.Shared.Persistence` and the `TestSupport` namespaces of the builders they use. The build names any that is missing.

| File | Namespace | Usings |
|---|---|---|
| `test/{Name}.Tests/Application/Customers/RegisterCompletedOrderWhenOrderCompletedEventHandlerTests.cs` | `{Name}.Tests.Application.Customers` | Moq, {Name}.Application.Customers, {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders.Events, {Name}.Tests.TestSupport.Domain.Customers, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Application/Orders/CancelOrderWhenOrderLineCancelledEventHandlerTests.cs` | `{Name}.Tests.Application.Orders` | Moq, {Name}.Application.Orders, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Orders.Events, {Name}.Tests.TestSupport.Domain.Orders, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Application/Orders/CreateOrderCommandHandlerTests.cs` | `{Name}.Tests.Application.Orders` | Moq, {Name}.Application.Orders, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Exceptions, {Name}.Shared.Persistence, {Name}.Tests.TestSupport.Domain.Orders, {Name}.Tests.TestSupport.Shared.Application, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/Application/Products/CreateProductCommandHandlerTests.cs` | `{Name}.Tests.Application.Products` | Moq, {Name}.Application.Products, {Name}.Domain.Aggregates.Products, {Name}.Shared.Exceptions, {Name}.Shared.Persistence, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Shared.Application, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/Application/Products/DecreaseProductStockCommandHandlerTests.cs` | `{Name}.Tests.Application.Products` | Moq, {Name}.Application.Products, {Name}.Domain.Aggregates.Products, {Name}.Shared.Exceptions, {Name}.Shared.Persistence, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Shared.Application, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/Application/Products/DecreaseStockWhenOrderLineAddedEventHandlerTests.cs` | `{Name}.Tests.Application.Products` | Moq, {Name}.Application.Products, {Name}.Domain.Aggregates.Orders.Events, {Name}.Domain.Aggregates.Products, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Application/Products/IncreaseProductStockCommandHandlerTests.cs` | `{Name}.Tests.Application.Products` | Moq, {Name}.Application.Products, {Name}.Domain.Aggregates.Products, {Name}.Shared.Exceptions, {Name}.Shared.Persistence, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Shared.Application, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/Application/Products/IncreaseStockWhenOrderLineCancelledEventHandlerTests.cs` | `{Name}.Tests.Application.Products` | Moq, {Name}.Application.Products, {Name}.Domain.Aggregates.Orders.Events, {Name}.Domain.Aggregates.Products, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Domain/Customers/CustomerTests.cs` | `{Name}.Tests.Domain.Customers` | {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Customers.Events, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Domain.Customers, {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/Domain/Orders/MoneyTests.cs` | `{Name}.Tests.Domain.Orders` | System.Globalization, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Exceptions |
| `test/{Name}.Tests/Domain/Orders/OrderTests.cs` | `{Name}.Tests.Domain.Orders` | {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Orders.Events, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Domain.Orders |
| `test/{Name}.Tests/Domain/Products/ProductTests.cs` | `{Name}.Tests.Domain.Products` | {Name}.Domain.Aggregates.Products, {Name}.Domain.Aggregates.Products.Events, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/Infrastructure/Persistence/Orders/OrderConfigurationTests.cs` | `{Name}.Tests.Infrastructure.Persistence.Orders` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata, {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders, {Name}.Tests.TestSupport.Infrastructure.Persistence |
| `test/{Name}.Tests/Infrastructure/Persistence/Orders/OrderRepositoryTests.cs` | `{Name}.Tests.Infrastructure.Persistence.Orders` | Microsoft.EntityFrameworkCore, Moq, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Orders.Events, {Name}.Infrastructure.Persistence.Orders, {Name}.Shared.Domain, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Domain.Orders, {Name}.Tests.TestSupport.Infrastructure.Persistence, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/Infrastructure/Persistence/Products/ProductConfigurationTests.cs` | `{Name}.Tests.Infrastructure.Persistence.Products` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Infrastructure, Microsoft.EntityFrameworkCore.Metadata, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Products, {Name}.Infrastructure.Persistence, {Name}.Tests.TestSupport.Infrastructure.Persistence |
| `test/{Name}.Tests/Infrastructure/Persistence/Products/ProductRepositoryTests.cs` | `{Name}.Tests.Infrastructure.Persistence.Products` | {Name}.Domain.Aggregates.Products.Events, {Name}.Infrastructure.Persistence.Products, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Infrastructure.Persistence, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/TestSupport/Domain/Customers/CustomerRepositoryBuilder.cs` | `{Name}.Tests.TestSupport.Domain.Customers` | Moq, {Name}.Domain.Aggregates.Customers |
| `test/{Name}.Tests/TestSupport/Domain/Customers/FakeCustomer.cs` | `{Name}.Tests.TestSupport.Domain.Customers` | {Name}.Domain.Aggregates.Customers, {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Domain/Orders/FakeOrder.cs` | `{Name}.Tests.TestSupport.Domain.Orders` | {Name}.Domain.Aggregates.Orders, {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs` | `{Name}.Tests.TestSupport.Domain.Orders` | Moq, {Name}.Domain.Aggregates.Orders |
| `test/{Name}.Tests/TestSupport/Domain/Products/FakeProduct.cs` | `{Name}.Tests.TestSupport.Domain.Products` | {Name}.Domain.Aggregates.Products, {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Domain/Products/ProductRepositoryBuilder.cs` | `{Name}.Tests.TestSupport.Domain.Products` | Moq, {Name}.Domain.Aggregates.Products |
