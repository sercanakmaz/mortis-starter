# The worked slice: the example domain, C#

Generated when `example` is `true`. Its tests are in `worked-slice-tests.md`. The domain is specified in roles in `mortis-architect/references/architecture/example-domain.md`; this page is its C# rendering, member by member. It is also the slice every new aggregate imitates. File paths are in `manifest.json`; names follow `naming.md`.

## The order of work

Event handlers of products and customers react to order events, so the domain comes first for all three, then the rest. Build after each line:

1. Domain of Product, then Customer, then Order (constants, value objects, child entities, the root, events, repository interface), with each one's test support (`Fake{Aggregate}`, `{Aggregate}RepositoryBuilder`) and domain tests.
2. Application of Order, then Customer, then Product (view, commands, event handlers, registration lines), each followed by its handler and event handler tests.
3. Infrastructure of the three (configuration, repository, registration line) and their tests.
4. The three controllers.

Then the Console scenarios and their registration, then the migration (`infrastructure-postgres.md` "Migrations").

## Products

### Domain

```csharp
public static class ProductConstants
```

```csharp
public const string InsufficientStockCode = "product_insufficient_stock";
```

```csharp
public const int SkuMaxLength = 64;
```

```csharp
public const int NameMaxLength = 200;
```

```csharp
public class Product : AggregateRoot<Guid>
```

```csharp
public string Sku { get; protected set; }
```

```csharp
public string Name { get; protected set; }
```

```csharp
public int Stock { get; protected set; }
```

```csharp
[DebuggerStepThrough]
protected Product() { }
```

```csharp
public Product(Guid id, string sku, string name, int stock, AuditUser user = null) : base(user)
{
    if (stock < 0)
    {
        throw new InvalidParameterException(nameof(stock), "it cannot be negative");
    }

    Id = id;
    Sku = sku;
    Name = name;
    Stock = stock;

    RaiseEvent(new Created(Id.ToString(), Sku));
}
```

```csharp
public void IncreaseStock(int quantity)
{
    if (quantity <= 0)
    {
        throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
    }

    Stock += quantity;

    RaiseEvent(new StockIncreased(Id.ToString(), Sku, quantity, Stock));
}
```

```csharp
public void DecreaseStock(int quantity)
{
    if (quantity <= 0)
    {
        throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
    }

    if (Stock < quantity)
    {
        throw new BusinessRuleException(ProductConstants.InsufficientStockCode, "The product does not have enough stock.");
    }

    Stock -= quantity;

    RaiseEvent(new StockDecreased(Id.ToString(), Sku, quantity, Stock));
}
```

Events, one file each in `Events/`:

```csharp
public record Created(string Id, string Sku) : EventBase;
```

```csharp
public record StockIncreased(string Id, string Sku, int Quantity, int Stock) : EventBase, IQueueEvent;
```

```csharp
public record StockDecreased(string Id, string Sku, int Quantity, int Stock) : EventBase, IQueueEvent;
```

```csharp
public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product> FindBySkuTrackingAsync(string sku);
}
```

### Application

```csharp
public record ProductDto
```

```csharp
public Guid Id { get; set; }
```

```csharp
public string Sku { get; set; }
```

```csharp
public string Name { get; set; }
```

```csharp
public int Stock { get; set; }
```

```csharp
public static ProductDto Map(Product product) => new()
{
    Id = product.Id,
    Sku = product.Sku,
    Name = product.Name,
    Stock = product.Stock
};
```

| Command | Properties | Validator | Handler loads | Calls | Saves |
|---|---|---|---|---|---|
| `CreateProductCommand` | `Id` (default `Guid.CreateVersion7()`), `Sku`, `Name`, `Stock` | id not empty; sku, name required within their lengths; stock `>= 0` | none | `new Product(command.Id, command.Sku, command.Name, command.Stock, command.ToAuditUser())` | `CreateAsync` |
| `GetProductCommand(Guid Id)` | | none | `FindByIdAsync` | | |
| `IncreaseProductStockCommand` | `Sku`, `Quantity` | sku required within length; quantity `> 0` | `FindBySkuTrackingAsync(command.Sku)` | `IncreaseStock(command.Quantity)` | `UpdateAsync` |
| `DecreaseProductStockCommand` | `Sku`, `Quantity` | as above | `FindBySkuTrackingAsync(command.Sku)` | `DecreaseStock(command.Quantity)` | `UpdateAsync` |

Each handler takes `IProductRepository productRepository` and the pipeline, and follows the shapes in `application.md`. Loading by sku:

```csharp
protected override async Task<CommandResult<ProductDto>> OnHandle(DecreaseProductStockCommand command)
{
    var product = await productRepository.FindBySkuTrackingAsync(command.Sku)
        ?? throw new NotFoundException(nameof(Product));

    product.DecreaseStock(command.Quantity);
    product.SetUpdatedAudit(command.ToAuditUser());

    await productRepository.UpdateAsync(product);

    return CommandResult.FromContent(ProductDto.Map(product));
}
```

```csharp
public DecreaseProductStockCommandValidator()
{
    RuleFor(command => command.Sku).NotEmpty().MaximumLength(ProductConstants.SkuMaxLength);
    RuleFor(command => command.Quantity).GreaterThan(0);
}
```

Event handlers (async, as the system user), in `Products/`:

```csharp
protected override async Task OnHandle(LineAdded @event)
{
    await commandDispatcher.Send(new DecreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, UserId = SystemUser.Id, UserEmail = SystemUser.Email });
}
```

```csharp
protected override async Task OnHandle(LineCancelled @event)
{
    await commandDispatcher.Send(new IncreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, UserId = SystemUser.Id, UserEmail = SystemUser.Email });
}
```

## Customers

### Domain

```csharp
public static class CustomerConstants
```

```csharp
public const string BlockedCode = "customer_blocked";
```

```csharp
public const int NameMaxLength = 200;
```

```csharp
public const int EmailMaxLength = 320;
```

```csharp
public class Customer : AggregateRoot<Guid>
```

```csharp
public string Name { get; protected set; }
```

```csharp
public string Email { get; protected set; }
```

```csharp
public bool IsBlocked { get; protected set; }
```

```csharp
public int CompletedOrderCount { get; protected set; }
```

```csharp
[DebuggerStepThrough]
protected Customer() { }
```

```csharp
public Customer(Guid id, string name, string email, AuditUser user = null) : base(user)
{
    Id = id;
    Name = name;
    Email = email.Trim().ToLowerInvariant();

    RaiseEvent(new Created(Id.ToString(), Email));
}
```

```csharp
public void Block()
{
    IsBlocked = true;

    RaiseEvent(new Blocked(Id.ToString()));
}
```

```csharp
public void RegisterCompletedOrder()
{
    if (IsBlocked)
    {
        throw new BusinessRuleException(CustomerConstants.BlockedCode, "A blocked customer cannot complete orders.");
    }

    CompletedOrderCount++;
}
```

```csharp
public record Created(string Id, string Email) : EventBase;
```

```csharp
public record Blocked(string Id) : EventBase;
```

```csharp
public interface ICustomerRepository : IRepository<Customer, Guid>;
```

### Application

```csharp
public record CustomerDto
```

```csharp
public Guid Id { get; set; }
```

```csharp
public string Name { get; set; }
```

```csharp
public string Email { get; set; }
```

```csharp
public bool IsBlocked { get; set; }
```

```csharp
public int CompletedOrderCount { get; set; }
```

```csharp
public static CustomerDto Map(Customer customer) => new()
{
    Id = customer.Id,
    Name = customer.Name,
    Email = customer.Email,
    IsBlocked = customer.IsBlocked,
    CompletedOrderCount = customer.CompletedOrderCount
};
```

| Command | Properties | Validator | Handler loads | Calls | Saves |
|---|---|---|---|---|---|
| `CreateCustomerCommand` | `Id` (default `Guid.CreateVersion7()`), `Name`, `Email` | id not empty; name required within length; email required, well formed, within length | none | `new Customer(command.Id, command.Name, command.Email, command.ToAuditUser())` | `CreateAsync` |
| `GetCustomerCommand(Guid Id)` | | none | `FindByIdAsync` | | |
| `BlockCustomerCommand(Guid Id)` | | none | `FindByIdTrackingAsync` | `Block()` | `UpdateAsync` |
| `RegisterCompletedOrderCommand(Guid Id)` | | none | `FindByIdTrackingAsync` | `RegisterCompletedOrder()` | `UpdateAsync` |

```csharp
public CreateCustomerCommandValidator()
{
    RuleFor(command => command.Id).NotEmpty();
    RuleFor(command => command.Name).NotEmpty().MaximumLength(CustomerConstants.NameMaxLength);
    RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(CustomerConstants.EmailMaxLength);
}
```

Event handler (sync, as the user of the event), in `Customers/`. The event is the order's `Completed`:

```csharp
protected override async Task OnHandle(Completed @event)
{
    await commandDispatcher.Send(new RegisterCompletedOrderCommand(Guid.Parse(@event.CustomerId)) { UserId = @event.UserId, UserEmail = @event.UserEmail });
}
```

## Orders

### Domain

```csharp
public static class OrderConstants
```

```csharp
public const string NotOpenCode = "order_not_open";
```

```csharp
public const string MixedCurrencyCode = "order_mixed_currency";
```

```csharp
public const string NothingToFulfilCode = "order_nothing_to_fulfil";
```

```csharp
public const string LineAlreadyCancelledCode = "order_line_already_cancelled";
```

```csharp
public const int OrderNumberMaxLength = 64;
```

```csharp
public const int SkuMaxLength = 64;
```

```csharp
public const int CurrencyLength = 3;
```

```csharp
public const int ReasonMaxLength = 500;
```

```csharp
namespace {Name}.Domain.Aggregates.Orders;

public enum OrderStatus
{
    Open,
    Completed,
    Cancelled
}
```

```csharp
public record Money
```

```csharp
public decimal Amount { get; }
```

```csharp
public string Currency { get; }
```

```csharp
public Money(decimal amount, string currency)
{
    if (amount < 0)
    {
        throw new InvalidParameterException(nameof(amount), "it cannot be negative");
    }

    if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != OrderConstants.CurrencyLength)
    {
        throw new InvalidParameterException(nameof(currency), "it must be a three-letter code");
    }

    Amount = amount;
    Currency = currency.Trim().ToUpperInvariant();
}
```

```csharp
public class OrderLine : Entity<Guid>
```

```csharp
public Guid OrderId { get; private init; }
```

```csharp
public string Sku { get; protected set; }
```

```csharp
public Money Price { get; protected set; }
```

```csharp
public int Quantity { get; protected set; }
```

```csharp
public bool IsCancelled { get; protected set; }
```

```csharp
public string CancellationReason { get; protected set; }
```

```csharp
[DebuggerStepThrough]
protected OrderLine() { }
```

```csharp
internal OrderLine(Guid id, Guid orderId, string sku, Money price, int quantity)
{
    Id = id;
    OrderId = orderId;
    Sku = sku;
    Price = price;
    Quantity = quantity;
}
```

```csharp
internal void Cancel(string reason)
{
    if (IsCancelled)
    {
        throw new BusinessRuleException(OrderConstants.LineAlreadyCancelledCode, "The order line is already cancelled.");
    }

    IsCancelled = true;
    CancellationReason = reason;
}
```

```csharp
public record OrderLineModel(string Sku, decimal Price, string Currency, int Quantity);
```

```csharp
public class Order : AggregateRoot<Guid>
```

```csharp
private readonly List<OrderLine> _lines = [];
```

```csharp
public string OrderNumber { get; protected set; }
```

```csharp
public Guid CustomerId { get; protected set; }
```

```csharp
public OrderStatus Status { get; protected set; }
```

```csharp
public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
```

```csharp
public decimal TotalAmount => _lines.Where(line => !line.IsCancelled).Sum(line => line.Price.Amount * line.Quantity);
```

```csharp
[DebuggerStepThrough]
protected Order() { }
```

```csharp
public Order(Guid id, string orderNumber, Guid customerId, AuditUser user = null) : base(user)
{
    Id = id;
    OrderNumber = orderNumber;
    CustomerId = customerId;
    Status = OrderStatus.Open;

    RaiseEvent(new Created(Id.ToString(), OrderNumber, CustomerId.ToString()));
}
```

```csharp
public OrderLine AddLine(string sku, Money price, int quantity)
{
    EnsureOpen();

    if (quantity <= 0)
    {
        throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
    }

    if (_lines.Any(line => !line.IsCancelled && line.Sku == sku))
    {
        throw new DuplicateException(nameof(OrderLine));
    }

    if (_lines.Count > 0 && _lines[0].Price.Currency != price.Currency)
    {
        throw new BusinessRuleException(OrderConstants.MixedCurrencyCode, "Every line of an order uses the same currency.");
    }

    var orderLine = new OrderLine(Guid.CreateVersion7(), Id, sku, price, quantity);

    _lines.Add(orderLine);

    RaiseEvent(new LineAdded(Id.ToString(), orderLine.Id.ToString(), sku, quantity));

    return orderLine;
}
```

```csharp
public void CancelLine(Guid lineId, string reason)
{
    EnsureOpen();

    var orderLine = _lines.FirstOrDefault(line => line.Id == lineId)
        ?? throw new NotFoundException(nameof(OrderLine));

    orderLine.Cancel(reason);

    RaiseEvent(new LineCancelled(Id.ToString(), lineId.ToString(), orderLine.Sku, orderLine.Quantity, reason));
}
```

```csharp
public bool CancelWhenAllLinesCancelled()
{
    if (Status != OrderStatus.Open || _lines.Any(line => !line.IsCancelled))
    {
        return false;
    }

    Status = OrderStatus.Cancelled;

    RaiseEvent(new Cancelled(Id.ToString(), OrderNumber, CustomerId.ToString()));

    return true;
}
```

```csharp
public void Complete()
{
    EnsureOpen();

    if (_lines.All(line => line.IsCancelled))
    {
        throw new BusinessRuleException(OrderConstants.NothingToFulfilCode, "An order without an active line cannot be fulfilled.");
    }

    Status = OrderStatus.Completed;

    RaiseEvent(new Completed(Id.ToString(), OrderNumber, CustomerId.ToString()));
}
```

```csharp
private void EnsureOpen()
{
    if (Status != OrderStatus.Open)
    {
        throw new BusinessRuleException(OrderConstants.NotOpenCode, "The order is not open.");
    }
}
```

```csharp
public record Created(string Id, string OrderNumber, string CustomerId) : EventBase;
```

```csharp
public record LineAdded(string Id, string LineId, string Sku, int Quantity) : EventBase, IQueueEvent;
```

```csharp
public record LineCancelled(string Id, string LineId, string Sku, int Quantity, string Reason) : EventBase, IQueueEvent;
```

```csharp
public record Completed(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
```

```csharp
public record Cancelled(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
```

```csharp
public interface IOrderRepository : IRepository<Order, Guid>;
```

### Application

```csharp
public record OrderDto
```

```csharp
public Guid Id { get; set; }
```

```csharp
public string OrderNumber { get; set; }
```

```csharp
public Guid CustomerId { get; set; }
```

```csharp
public string Status { get; set; }
```

```csharp
public decimal TotalAmount { get; set; }
```

```csharp
public List<OrderLineDto> Lines { get; set; }
```

```csharp
public static OrderDto Map(Order order) => new()
{
    Id = order.Id,
    OrderNumber = order.OrderNumber,
    CustomerId = order.CustomerId,
    Status = order.Status.ToString(),
    TotalAmount = order.TotalAmount,
    Lines = order.Lines.Select(orderLine => OrderLineDto.Map(orderLine)).ToList()
};
```

```csharp
public record OrderLineDto
```

```csharp
public Guid Id { get; set; }
```

```csharp
public string Sku { get; set; }
```

```csharp
public decimal Price { get; set; }
```

```csharp
public string Currency { get; set; }
```

```csharp
public int Quantity { get; set; }
```

```csharp
public bool IsCancelled { get; set; }
```

```csharp
public string CancellationReason { get; set; }
```

```csharp
public static OrderLineDto Map(OrderLine orderLine) => new()
{
    Id = orderLine.Id,
    Sku = orderLine.Sku,
    Price = orderLine.Price.Amount,
    Currency = orderLine.Price.Currency,
    Quantity = orderLine.Quantity,
    IsCancelled = orderLine.IsCancelled,
    CancellationReason = orderLine.CancellationReason
};
```

| Command | Properties | Handler loads | Calls | Saves |
|---|---|---|---|---|
| `CreateOrderCommand` | `Id` (default), `OrderNumber`, `CustomerId`, `List<OrderLineModel> Lines` | none | `new Order(...)`, then `AddLine` per line | `CreateAsync` |
| `GetOrderCommand(Guid Id)` | | `FindByIdAsync` | | |
| `AddOrderLineCommand` | `Id`, `OrderLineModel Line` | `FindByIdTrackingAsync` | `AddLine(command.Line.Sku, new Money(command.Line.Price, command.Line.Currency), command.Line.Quantity)` | `UpdateAsync` |
| `CancelOrderLineCommand` | `Id`, `LineId`, `Reason` | `FindByIdTrackingAsync` | `CancelLine(command.LineId, command.Reason)` | `UpdateAsync` |
| `CompleteOrderCommand(Guid Id)` | | `FindByIdTrackingAsync` | `Complete()` | `UpdateAsync` |
| `CancelOrderWhenAllLinesCancelledCommand(Guid Id)` | | `FindByIdTrackingAsync` | `CancelWhenAllLinesCancelled()` | `UpdateAsync` only when it returned `true` |

The handlers of `CreateOrderCommand`, `CompleteOrderCommand`, `GetOrderCommand` and `CancelOrderWhenAllLinesCancelledCommand` are written out in `application.md`.

```csharp
public CreateOrderCommandValidator()
{
    RuleFor(command => command.Id).NotEmpty();
    RuleFor(command => command.OrderNumber).NotEmpty().MaximumLength(OrderConstants.OrderNumberMaxLength);
    RuleFor(command => command.CustomerId).NotEmpty();
    RuleFor(command => command.Lines).NotEmpty();

    RuleForEach(command => command.Lines).ChildRules(line =>
    {
        line.RuleFor(model => model.Sku).NotEmpty().MaximumLength(OrderConstants.SkuMaxLength);
        line.RuleFor(model => model.Price).GreaterThanOrEqualTo(0);
        line.RuleFor(model => model.Currency).NotEmpty().Length(OrderConstants.CurrencyLength);
        line.RuleFor(model => model.Quantity).GreaterThan(0);
    });
}
```

```csharp
public AddOrderLineCommandValidator()
{
    RuleFor(command => command.Id).NotEmpty();
    RuleFor(command => command.Line).NotNull();

    When(command => command.Line is not null, () =>
    {
        RuleFor(command => command.Line.Sku).NotEmpty().MaximumLength(OrderConstants.SkuMaxLength);
        RuleFor(command => command.Line.Price).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Line.Currency).NotEmpty().Length(OrderConstants.CurrencyLength);
        RuleFor(command => command.Line.Quantity).GreaterThan(0);
    });
}
```

```csharp
public CancelOrderLineCommandValidator()
{
    RuleFor(command => command.Id).NotEmpty();
    RuleFor(command => command.LineId).NotEmpty();
    RuleFor(command => command.Reason).NotEmpty().MaximumLength(OrderConstants.ReasonMaxLength);
}
```

Event handler (sync, as the user of the event), in `Orders/`:

```csharp
protected override async Task OnHandle(LineCancelled @event)
{
    await commandDispatcher.Send(new CancelOrderWhenAllLinesCancelledCommand(Guid.Parse(@event.Id)) { UserId = @event.UserId, UserEmail = @event.UserEmail });
}
```

### Registration

```csharp
public static IServiceCollection AddApplicationModule(this IServiceCollection services)
{
    services.AddCommandHandler<CreateCustomerCommand, CustomerDto, CreateCustomerCommandHandler>();
    services.AddScoped<IValidator<CreateCustomerCommand>, CreateCustomerCommandValidator>();
    services.AddCommandHandler<BlockCustomerCommand, CustomerDto, BlockCustomerCommandHandler>();
    services.AddCommandHandler<GetCustomerCommand, CustomerDto, GetCustomerCommandHandler>();
    services.AddCommandHandler<RegisterCompletedOrderCommand, CustomerDto, RegisterCompletedOrderCommandHandler>();
    services.AddScoped<BaseSyncEventHandler<Completed>, RegisterCompletedOrderWhenOrderCompletedEventHandler>();

    services.AddCommandHandler<CreateOrderCommand, OrderDto, CreateOrderCommandHandler>();
    services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
    services.AddCommandHandler<AddOrderLineCommand, OrderDto, AddOrderLineCommandHandler>();
    services.AddScoped<IValidator<AddOrderLineCommand>, AddOrderLineCommandValidator>();
    services.AddCommandHandler<CancelOrderLineCommand, OrderDto, CancelOrderLineCommandHandler>();
    services.AddScoped<IValidator<CancelOrderLineCommand>, CancelOrderLineCommandValidator>();
    services.AddCommandHandler<CompleteOrderCommand, OrderDto, CompleteOrderCommandHandler>();
    services.AddCommandHandler<GetOrderCommand, OrderDto, GetOrderCommandHandler>();
    services.AddCommandHandler<CancelOrderWhenAllLinesCancelledCommand, OrderDto, CancelOrderWhenAllLinesCancelledCommandHandler>();
    services.AddScoped<BaseSyncEventHandler<LineCancelled>, CancelOrderWhenOrderLineCancelledEventHandler>();

    services.AddCommandHandler<CreateProductCommand, ProductDto, CreateProductCommandHandler>();
    services.AddScoped<IValidator<CreateProductCommand>, CreateProductCommandValidator>();
    services.AddCommandHandler<GetProductCommand, ProductDto, GetProductCommandHandler>();
    services.AddCommandHandler<IncreaseProductStockCommand, ProductDto, IncreaseProductStockCommandHandler>();
    services.AddScoped<IValidator<IncreaseProductStockCommand>, IncreaseProductStockCommandValidator>();
    services.AddScoped<BaseAsyncEventHandler<LineCancelled>, IncreaseStockWhenOrderLineCancelledEventHandler>();
    services.AddCommandHandler<DecreaseProductStockCommand, ProductDto, DecreaseProductStockCommandHandler>();
    services.AddScoped<IValidator<DecreaseProductStockCommand>, DecreaseProductStockCommandValidator>();
    services.AddScoped<BaseAsyncEventHandler<LineAdded>, DecreaseStockWhenOrderLineAddedEventHandler>();

    return services;
}
```

## Infrastructure

```csharp
public void Configure(EntityTypeBuilder<Product> builder)
{
    builder.AggregateConfiguration<Product, Guid>();

    builder.Property(product => product.Sku).HasMaxLength(ProductConstants.SkuMaxLength).IsRequired();
    builder.Property(product => product.Name).HasMaxLength(ProductConstants.NameMaxLength).IsRequired();
    builder.Property(product => product.Stock).IsRequired();

    builder.HasAlternateKey(product => product.Sku);

    builder.Property<uint>(PersistenceConstants.RowVersionColumn).IsRowVersion();

    builder.ToTable("products", table => table.HasCheckConstraint("ck_products_stock_not_negative", "stock >= 0"));
}
```

```csharp
public void Configure(EntityTypeBuilder<Customer> builder)
{
    builder.AggregateConfiguration<Customer, Guid>();

    builder.Property(customer => customer.Name).HasMaxLength(CustomerConstants.NameMaxLength).IsRequired();
    builder.Property(customer => customer.Email).HasMaxLength(CustomerConstants.EmailMaxLength).IsRequired();

    builder.HasIndex(customer => customer.Email).IsUnique();

    builder.ToTable("customers");
}
```

The order and order-line configurations are in `infrastructure-postgres.md`.

`CustomerRepository` and `OrderRepository` add nothing to `EfRepository`. `ProductRepository` adds `FindBySkuTrackingAsync` (`infrastructure-postgres.md`).

```csharp
public static IServiceCollection AddPersistenceModule(this IServiceCollection services)
{
    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();

    return services;
}
```

## Controllers

```csharp
[ApiController]
[Route("products")]
public class ProductsController(ICommandDispatcher commandDispatcher) : ControllerBase
```

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
{
    var commandResult = await commandDispatcher.Send(command);

    return Created($"products/{commandResult.Content.Id}", commandResult.Content);
}
```

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> Get([FromRoute] Guid id)
{
    var commandResult = await commandDispatcher.Send(new GetProductCommand(id));

    return Ok(commandResult.Content);
}
```

```csharp
[ApiController]
[Route("customers")]
public class CustomersController(ICommandDispatcher commandDispatcher) : ControllerBase
```

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
{
    var commandResult = await commandDispatcher.Send(command);

    return Created($"customers/{commandResult.Content.Id}", commandResult.Content);
}
```

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> Get([FromRoute] Guid id)
{
    var commandResult = await commandDispatcher.Send(new GetCustomerCommand(id));

    return Ok(commandResult.Content);
}
```

```csharp
[HttpPut("{id:guid}/block")]
public async Task<IActionResult> Block([FromRoute] Guid id)
{
    var commandResult = await commandDispatcher.Send(new BlockCustomerCommand(id));

    return Ok(commandResult.Content);
}
```

```csharp
[ApiController]
[Route("orders")]
public class OrdersController(ICommandDispatcher commandDispatcher) : ControllerBase
```

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateOrderCommand command)
{
    var commandResult = await commandDispatcher.Send(command);

    return Created($"orders/{commandResult.Content.Id}", commandResult.Content);
}
```

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> Get([FromRoute] Guid id)
{
    var commandResult = await commandDispatcher.Send(new GetOrderCommand(id));

    return Ok(commandResult.Content);
}
```

```csharp
[HttpPost("{id:guid}/lines")]
public async Task<IActionResult> AddLine([FromRoute] Guid id, [FromBody] AddOrderLineCommand command)
{
    var commandResult = await commandDispatcher.Send(command with { Id = id });

    return Ok(commandResult.Content);
}
```

```csharp
[HttpPut("{id:guid}/lines/{lineId:guid}/cancel")]
public async Task<IActionResult> CancelLine([FromRoute] Guid id, [FromRoute] Guid lineId, [FromBody] CancelOrderLineCommand command)
{
    var commandResult = await commandDispatcher.Send(command with { Id = id, LineId = lineId });

    return Ok(commandResult.Content);
}
```

```csharp
[HttpPut("{id:guid}/complete")]
public async Task<IActionResult> Complete([FromRoute] Guid id)
{
    var commandResult = await commandDispatcher.Send(new CompleteOrderCommand(id));

    return Ok(commandResult.Content);
}
```

## Console scenarios

```csharp
public class OrderFlowScenario(ICommandDispatcher commandDispatcher) : IScenario
```

```csharp
public string Name => "order-flow";
```

```csharp
public async Task RunAsync(string[] args)
{
    var suffix = Guid.CreateVersion7().ToString("N")[^8..];

    var firstProduct = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-1-{suffix}", Name = "First product", Stock = 100 }.AsConsoleUser())).Content;
    var secondProduct = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-2-{suffix}", Name = "Second product", Stock = 100 }.AsConsoleUser())).Content;
    var thirdProduct = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-3-{suffix}", Name = "Third product", Stock = 100 }.AsConsoleUser())).Content;

    var customer = (await commandDispatcher.Send(new CreateCustomerCommand
    {
        Name = "Console Customer",
        Email = $"customer-{suffix}@example.com"
    }.AsConsoleUser())).Content;

    var order = (await commandDispatcher.Send(new CreateOrderCommand
    {
        OrderNumber = $"ORD-{suffix}",
        CustomerId = customer.Id,
        Lines =
        [
            new OrderLineModel(firstProduct.Sku, 10.50m, "eur", 2),
            new OrderLineModel(secondProduct.Sku, 4.00m, "EUR", 1)
        ]
    }.AsConsoleUser())).Content;

    var orderWithAddedLine = (await commandDispatcher.Send(new AddOrderLineCommand
    {
        Id = order.Id,
        Line = new OrderLineModel(thirdProduct.Sku, 1.25m, "EUR", 4)
    }.AsConsoleUser())).Content;

    await commandDispatcher.Send(new CancelOrderLineCommand
    {
        Id = order.Id,
        LineId = orderWithAddedLine.Lines.First(orderLine => orderLine.Sku == secondProduct.Sku).Id,
        Reason = "out of stock"
    }.AsConsoleUser());

    var completedOrder = (await commandDispatcher.Send(new CompleteOrderCommand(order.Id).AsConsoleUser())).Content;
    var customerAfterCompletion = (await commandDispatcher.Send(new GetCustomerCommand(customer.Id))).Content;
    var firstProductAfterCompletion = (await commandDispatcher.Send(new GetProductCommand(firstProduct.Id))).Content;
    var secondProductAfterCompletion = (await commandDispatcher.Send(new GetProductCommand(secondProduct.Id))).Content;
    var thirdProductAfterCompletion = (await commandDispatcher.Send(new GetProductCommand(thirdProduct.Id))).Content;

    System.Console.WriteLine(JsonSerializer.Serialize(new
    {
        Order = completedOrder,
        Customer = customerAfterCompletion,
        Products = new[] { firstProductAfterCompletion, secondProductAfterCompletion, thirdProductAfterCompletion }
    }, ScenarioConstants.OutputOptions));
}
```

```csharp
public class BlockedCustomerScenario(ICommandDispatcher commandDispatcher) : IScenario
```

```csharp
public string Name => "blocked-customer";
```

```csharp
public async Task RunAsync(string[] args)
{
    var suffix = Guid.CreateVersion7().ToString("N")[^8..];

    var product = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-{suffix}", Name = "Product", Stock = 10 }.AsConsoleUser())).Content;

    var customer = (await commandDispatcher.Send(new CreateCustomerCommand
    {
        Name = "Blocked Customer",
        Email = $"blocked-{suffix}@example.com"
    }.AsConsoleUser())).Content;

    var order = (await commandDispatcher.Send(new CreateOrderCommand
    {
        OrderNumber = $"BLK-{suffix}",
        CustomerId = customer.Id,
        Lines = [new OrderLineModel(product.Sku, 25.00m, "EUR", 1)]
    }.AsConsoleUser())).Content;

    await commandDispatcher.Send(new BlockCustomerCommand(customer.Id).AsConsoleUser());

    string rejectedWith = null;

    try
    {
        await commandDispatcher.Send(new CompleteOrderCommand(order.Id).AsConsoleUser());
    }
    catch (BusinessRuleException exception)
    {
        rejectedWith = exception.Code;
    }

    var orderAfterRejection = (await commandDispatcher.Send(new GetOrderCommand(order.Id))).Content;
    var customerAfterRejection = (await commandDispatcher.Send(new GetCustomerCommand(customer.Id))).Content;

    System.Console.WriteLine(JsonSerializer.Serialize(new { RejectedWith = rejectedWith, Order = orderAfterRejection, Customer = customerAfterRejection }, ScenarioConstants.OutputOptions));
}
```

```csharp
public class StockFlowScenario(ICommandDispatcher commandDispatcher) : IScenario
```

```csharp
public string Name => "stock-flow";
```

```csharp
public async Task RunAsync(string[] args)
{
    var suffix = Guid.CreateVersion7().ToString("N")[^8..];

    var bolt = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"BOLT-{suffix}", Name = "Bolt", Stock = 10 }.AsConsoleUser())).Content;
    var nut = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"NUT-{suffix}", Name = "Nut", Stock = 5 }.AsConsoleUser())).Content;

    var customer = (await commandDispatcher.Send(new CreateCustomerCommand
    {
        Name = "Stock Customer",
        Email = $"stock-{suffix}@example.com"
    }.AsConsoleUser())).Content;

    var order = (await commandDispatcher.Send(new CreateOrderCommand
    {
        OrderNumber = $"STK-{suffix}",
        CustomerId = customer.Id,
        Lines = [new OrderLineModel(bolt.Sku, 1.00m, "EUR", 3), new OrderLineModel(nut.Sku, 0.50m, "EUR", 2)]
    }.AsConsoleUser())).Content;

    var boltAfterOrder = (await commandDispatcher.Send(new GetProductCommand(bolt.Id))).Content;
    var nutAfterOrder = (await commandDispatcher.Send(new GetProductCommand(nut.Id))).Content;

    await commandDispatcher.Send(new CancelOrderLineCommand
    {
        Id = order.Id,
        LineId = order.Lines.First(orderLine => orderLine.Sku == bolt.Sku).Id,
        Reason = "changed mind"
    }.AsConsoleUser());

    var orderAfterFirstCancel = (await commandDispatcher.Send(new GetOrderCommand(order.Id))).Content;
    var boltAfterFirstCancel = (await commandDispatcher.Send(new GetProductCommand(bolt.Id))).Content;

    await commandDispatcher.Send(new CancelOrderLineCommand
    {
        Id = order.Id,
        LineId = order.Lines.First(orderLine => orderLine.Sku == nut.Sku).Id,
        Reason = "changed mind"
    }.AsConsoleUser());

    var orderAfterLastCancel = (await commandDispatcher.Send(new GetOrderCommand(order.Id))).Content;
    var nutAfterLastCancel = (await commandDispatcher.Send(new GetProductCommand(nut.Id))).Content;

    var oversoldOrder = (await commandDispatcher.Send(new CreateOrderCommand
    {
        OrderNumber = $"OVR-{suffix}",
        CustomerId = customer.Id,
        Lines = [new OrderLineModel(bolt.Sku, 1.00m, "EUR", 20)]
    }.AsConsoleUser())).Content;

    var oversoldOrderAfterwards = (await commandDispatcher.Send(new GetOrderCommand(oversoldOrder.Id))).Content;
    var boltAfterOversell = (await commandDispatcher.Send(new GetProductCommand(bolt.Id))).Content;
```

The member continues:

```csharp
    System.Console.WriteLine(JsonSerializer.Serialize(new
    {
        AfterOrder = new { Bolt = boltAfterOrder.Stock, Nut = nutAfterOrder.Stock },
        AfterFirstCancel = new { OrderStatus = orderAfterFirstCancel.Status, Bolt = boltAfterFirstCancel.Stock },
        AfterLastCancel = new { OrderStatus = orderAfterLastCancel.Status, Nut = nutAfterLastCancel.Stock },
        Oversell = new { OrderStatus = oversoldOrderAfterwards.Status, Bolt = boltAfterOversell.Stock }
    }, ScenarioConstants.OutputOptions));
}
```

```csharp
public static IServiceCollection AddScenariosModule(this IServiceCollection services)
{
    services.AddScoped<IScenario, OrderFlowScenario>();
    services.AddScoped<IScenario, StockFlowScenario>();
    services.AddScoped<IScenario, BlockedCustomerScenario>();

    services.AddCommandScenario<CreateCustomerCommand, CustomerDto>();
    services.AddCommandScenario<BlockCustomerCommand, CustomerDto>();
    services.AddCommandScenario<GetCustomerCommand, CustomerDto>();
    services.AddCommandScenario<RegisterCompletedOrderCommand, CustomerDto>();

    services.AddCommandScenario<CreateOrderCommand, OrderDto>();
    services.AddCommandScenario<AddOrderLineCommand, OrderDto>();
    services.AddCommandScenario<CancelOrderLineCommand, OrderDto>();
    services.AddCommandScenario<CompleteOrderCommand, OrderDto>();
    services.AddCommandScenario<GetOrderCommand, OrderDto>();
    services.AddCommandScenario<CancelOrderWhenAllLinesCancelledCommand, OrderDto>();

    services.AddCommandScenario<CreateProductCommand, ProductDto>();
    services.AddCommandScenario<GetProductCommand, ProductDto>();
    services.AddCommandScenario<IncreaseProductStockCommand, ProductDto>();
    services.AddCommandScenario<DecreaseProductStockCommand, ProductDto>();

    return services;
}
```

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.


A handler written to `application.md`'s shapes also needs `{Name}.Shared.Exceptions` (for `NotFoundException`); the handler and event handler tests need `Moq`, `{Name}.Shared.Exceptions`, `{Name}.Shared.Persistence` and the `TestSupport` namespaces of the builders they use. The build names any that is missing.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Application/Customers/BlockCustomerCommand.cs` | `{Name}.Application.Customers` | System.Text.Json.Serialization, {Name}.Domain.Aggregates.Customers, {Name}.Shared.Application |
| `src/{Name}.Application/Customers/CreateCustomerCommand.cs` | `{Name}.Application.Customers` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Customers, {Name}.Shared.Application |
| `src/{Name}.Application/Customers/CustomerDto.cs` | `{Name}.Application.Customers` | {Name}.Domain.Aggregates.Customers |
| `src/{Name}.Application/Customers/GetCustomerCommand.cs` | `{Name}.Application.Customers` | {Name}.Domain.Aggregates.Customers, {Name}.Shared.Application |
| `src/{Name}.Application/Customers/RegisterCompletedOrderCommand.cs` | `{Name}.Application.Customers` | System.Text.Json.Serialization, {Name}.Domain.Aggregates.Customers, {Name}.Shared.Application |
| `src/{Name}.Application/Customers/RegisterCompletedOrderWhenOrderCompletedEventHandler.cs` | `{Name}.Application.Customers` | {Name}.Domain.Aggregates.Orders.Events, {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Application/Orders/AddOrderLineCommand.cs` | `{Name}.Application.Orders` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Application/Orders/CancelOrderLineCommand.cs` | `{Name}.Application.Orders` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Application/Orders/CancelOrderWhenAllLinesCancelledCommand.cs` | `{Name}.Application.Orders` | System.Text.Json.Serialization, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Application/Orders/CancelOrderWhenOrderLineCancelledEventHandler.cs` | `{Name}.Application.Orders` | {Name}.Domain.Aggregates.Orders.Events, {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Application/Orders/CompleteOrderCommand.cs` | `{Name}.Application.Orders` | System.Text.Json.Serialization, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Application/Orders/CreateOrderCommand.cs` | `{Name}.Application.Orders` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Application/Orders/GetOrderCommand.cs` | `{Name}.Application.Orders` | {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Application/Orders/OrderDto.cs` | `{Name}.Application.Orders` | {Name}.Domain.Aggregates.Orders |
| `src/{Name}.Application/Products/CreateProductCommand.cs` | `{Name}.Application.Products` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Products, {Name}.Shared.Application |
| `src/{Name}.Application/Products/DecreaseProductStockCommand.cs` | `{Name}.Application.Products` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Products, {Name}.Shared.Application |
| `src/{Name}.Application/Products/DecreaseStockWhenOrderLineAddedEventHandler.cs` | `{Name}.Application.Products` | {Name}.Domain.Aggregates.Orders.Events, {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Application/Products/GetProductCommand.cs` | `{Name}.Application.Products` | {Name}.Domain.Aggregates.Products, {Name}.Shared.Application |
| `src/{Name}.Application/Products/IncreaseProductStockCommand.cs` | `{Name}.Application.Products` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Products, {Name}.Shared.Application |
| `src/{Name}.Application/Products/IncreaseStockWhenOrderLineCancelledEventHandler.cs` | `{Name}.Application.Products` | {Name}.Domain.Aggregates.Orders.Events, {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Application/Products/ProductDto.cs` | `{Name}.Application.Products` | {Name}.Domain.Aggregates.Products |
| `src/{Name}.Domain/Aggregates/Customers/Customer.cs` | `{Name}.Domain.Aggregates.Customers` | System.Diagnostics, {Name}.Domain.Aggregates.Customers.Events, {Name}.Shared.Domain, {Name}.Shared.Exceptions |
| `src/{Name}.Domain/Aggregates/Customers/CustomerConstants.cs` | `{Name}.Domain.Aggregates.Customers` | none |
| `src/{Name}.Domain/Aggregates/Customers/Events/Blocked.cs` | `{Name}.Domain.Aggregates.Customers.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Customers/Events/Created.cs` | `{Name}.Domain.Aggregates.Customers.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Customers/ICustomerRepository.cs` | `{Name}.Domain.Aggregates.Customers` | {Name}.Shared.Persistence |
| `src/{Name}.Domain/Aggregates/Orders/Events/Cancelled.cs` | `{Name}.Domain.Aggregates.Orders.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Orders/Events/Completed.cs` | `{Name}.Domain.Aggregates.Orders.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Orders/Events/Created.cs` | `{Name}.Domain.Aggregates.Orders.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Orders/Events/LineAdded.cs` | `{Name}.Domain.Aggregates.Orders.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Orders/Events/LineCancelled.cs` | `{Name}.Domain.Aggregates.Orders.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Orders/IOrderRepository.cs` | `{Name}.Domain.Aggregates.Orders` | {Name}.Shared.Persistence |
| `src/{Name}.Domain/Aggregates/Orders/Money.cs` | `{Name}.Domain.Aggregates.Orders` | {Name}.Shared.Exceptions |
| `src/{Name}.Domain/Aggregates/Orders/Order.cs` | `{Name}.Domain.Aggregates.Orders` | System.Diagnostics, {Name}.Domain.Aggregates.Orders.Events, {Name}.Shared.Domain, {Name}.Shared.Exceptions |
| `src/{Name}.Domain/Aggregates/Orders/OrderConstants.cs` | `{Name}.Domain.Aggregates.Orders` | none |
| `src/{Name}.Domain/Aggregates/Orders/OrderLine.cs` | `{Name}.Domain.Aggregates.Orders` | System.Diagnostics, {Name}.Shared.Domain, {Name}.Shared.Exceptions |
| `src/{Name}.Domain/Aggregates/Orders/OrderLineModel.cs` | `{Name}.Domain.Aggregates.Orders` | none |
| `src/{Name}.Domain/Aggregates/Orders/OrderStatus.cs` | `{Name}.Domain.Aggregates.Orders` | none |
| `src/{Name}.Domain/Aggregates/Products/Events/Created.cs` | `{Name}.Domain.Aggregates.Products.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Products/Events/StockDecreased.cs` | `{Name}.Domain.Aggregates.Products.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Products/Events/StockIncreased.cs` | `{Name}.Domain.Aggregates.Products.Events` | {Name}.Shared.Domain |
| `src/{Name}.Domain/Aggregates/Products/IProductRepository.cs` | `{Name}.Domain.Aggregates.Products` | {Name}.Shared.Persistence |
| `src/{Name}.Domain/Aggregates/Products/Product.cs` | `{Name}.Domain.Aggregates.Products` | System.Diagnostics, {Name}.Domain.Aggregates.Products.Events, {Name}.Shared.Domain, {Name}.Shared.Exceptions |
| `src/{Name}.Domain/Aggregates/Products/ProductConstants.cs` | `{Name}.Domain.Aggregates.Products` | none |
