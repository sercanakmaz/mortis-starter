# Application: commands, handlers, views, event handlers, C#

Project `{Name}.Application`. References Domain and Shared. Layer 1: `layers.md` "Application", `building-blocks.md` Command, Validator, Command handler, DTO, Sync and Async event handler; the decision cheat sheet in `placement-guide.md`.

## The command file

One file per command, `{Plural}/{Action}{Aggregate}Command.cs`, holding three types in this order: the command, its validator (if it has rules), its handler.

### The command

A `record` with init-able public properties (`{ get; set; }`), so both JSON and `with` work.

```csharp
public record CreateOrderCommand : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
```

```csharp
[JsonIgnore] public string UserId { get; set; }
```

```csharp
[JsonIgnore] public string UserEmail { get; set; }
```

```csharp
public Guid Id { get; set; } = Guid.CreateVersion7();
```

```csharp
public List<OrderLineModel> Lines { get; set; }
```

- A command that writes implements `ICommand<TView>, IUserCommand, ITransactionalCommand` and declares `UserId` and `UserEmail` with `[JsonIgnore]` (`using System.Text.Json.Serialization;`).
- A create command carries its new id with a default of `Guid.CreateVersion7()`, so the client may send one.
- A command with only an id is positional, and still declares the user properties in its body:

```csharp
public record CompleteOrderCommand(Guid Id) : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
```

```csharp
[JsonIgnore] public string UserId { get; set; }
```

```csharp
[JsonIgnore] public string UserEmail { get; set; }
```

- A read is `Get{Aggregate}Command(Guid Id) : ICommand<{Aggregate}Dto>`: no user, no transaction.

### The validator

`AbstractValidator<TCommand>` (`using FluentValidation;`), checking only what the command alone shows: required, format, length (from `{Aggregate}Constants`), range.

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

A nested object that may be missing:

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

### The handler

```csharp
public class CompleteOrderCommandHandler(
    IOrderRepository orderRepository,
    CommandPipeline<CompleteOrderCommand> commandPipeline) : BaseCommandHandler<CompleteOrderCommand, OrderDto>(commandPipeline)
```

The handler is the use case. It takes the repositories (and domain services) it needs and the pipeline; it loads, calls the aggregate, stamps the user, saves, maps. It has exactly one method, `OnHandle`, and no private methods. The shapes:

**Operate on one aggregate by id:**

```csharp
protected override async Task<CommandResult<OrderDto>> OnHandle(CompleteOrderCommand command)
{
    var order = await orderRepository.FindByIdTrackingAsync(command.Id)
        ?? throw new NotFoundException(nameof(Order));

    order.Complete();
    order.SetUpdatedAudit(command.ToAuditUser());

    await orderRepository.UpdateAsync(order);

    return CommandResult.FromContent(OrderDto.Map(order));
}
```

**Create:**

```csharp
protected override async Task<CommandResult<OrderDto>> OnHandle(CreateOrderCommand command)
{
    var order = new Order(command.Id, command.OrderNumber, command.CustomerId, command.ToAuditUser());

    foreach (var line in command.Lines)
    {
        order.AddLine(line.Sku, new Money(line.Price, line.Currency), line.Quantity);
    }

    await orderRepository.CreateAsync(order);

    return CommandResult.FromContent(OrderDto.Map(order));
}
```

**Read:**

```csharp
protected override async Task<CommandResult<OrderDto>> OnHandle(GetOrderCommand command)
{
    var order = await orderRepository.FindByIdAsync(command.Id)
        ?? throw new NotFoundException(nameof(Order));

    return CommandResult.FromContent(OrderDto.Map(order));
}
```

**Save only when the operation changed something** (a reaction that may run when there is nothing to do):

```csharp
protected override async Task<CommandResult<OrderDto>> OnHandle(CancelOrderWhenAllLinesCancelledCommand command)
{
    var order = await orderRepository.FindByIdTrackingAsync(command.Id)
        ?? throw new NotFoundException(nameof(Order));

    if (order.CancelWhenAllLinesCancelled())
    {
        order.SetUpdatedAudit(command.ToAuditUser());

        await orderRepository.UpdateAsync(order);
    }

    return CommandResult.FromContent(OrderDto.Map(order));
}
```

**Two aggregates the action is about** (only when the second change is the intent, not a consequence; see the cheat sheet):

```csharp
protected override async Task<CommandResult<TransferDto>> OnHandle(TransferStockCommand command)
{
    var source = await warehouseRepository.FindByIdTrackingAsync(command.SourceId)
        ?? throw new NotFoundException(nameof(Warehouse));
    var target = await warehouseRepository.FindByIdTrackingAsync(command.TargetId)
        ?? throw new NotFoundException(nameof(Warehouse));

    source.Release(command.Sku, command.Quantity);
    target.Receive(command.Sku, command.Quantity);
    source.SetUpdatedAudit(command.ToAuditUser());
    target.SetUpdatedAudit(command.ToAuditUser());

    await warehouseRepository.UpdateAsync(source);
    await warehouseRepository.UpdateAsync(target);

    return CommandResult.FromContent(TransferDto.Map(source, target));
}
```

The transfer is an illustration; the example domain has no command of this shape.

- `SetUpdatedAudit` comes after the operation, so the operation's events carry the user.
- The handler never catches a business failure and never decides with an `if` of its own; the one `if` allowed is on an operation's own "did it change" answer.
- A consequence in another aggregate is an event handler, never another line here.

## The view

File `{Plural}/{Aggregate}Dto.cs`. A record with settable properties and a static `Map` that names every field it reads. A child view sits in the same file.

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

- An enum is mapped with `.ToString()`. No mapping library.

## Event handlers

File `{ReactingPlural}/{Action}When{Aggregate}{Event}EventHandler.cs`, in the folder of the aggregate that reacts. Body: one awaited `Send`. The event's own namespace is imported with an alias when another aggregate has an event of the same name.

A sync handler runs as the user of the event:

```csharp
public class CancelOrderWhenOrderLineCancelledEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<LineCancelled>
```

```csharp
protected override async Task OnHandle(LineCancelled @event)
{
    await commandDispatcher.Send(new CancelOrderWhenAllLinesCancelledCommand(Guid.Parse(@event.Id)) { UserId = @event.UserId, UserEmail = @event.UserEmail });
}
```

An async handler runs as the system user:

```csharp
protected override async Task OnHandle(LineAdded @event)
{
    await commandDispatcher.Send(new DecreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, UserId = SystemUser.Id, UserEmail = SystemUser.Email });
}
```

## Registration

File `ApplicationModule.cs`, namespace `{Name}.Application`. Grouped by aggregate; inside a group, per command: the handler, then its validator; then the group's event handlers. Needs `using FluentValidation;`, `using {Name}.Shared.Application;` and `using {Name}.Shared.Domain;`.

```csharp
public static class ApplicationModule
```

```csharp
services.AddCommandHandler<CreateOrderCommand, OrderDto, CreateOrderCommandHandler>();
services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
services.AddScoped<BaseSyncEventHandler<LineCancelled>, CancelOrderWhenOrderLineCancelledEventHandler>();
services.AddScoped<BaseAsyncEventHandler<LineAdded>, DecreaseStockWhenOrderLineAddedEventHandler>();
```

Written first with an empty body, and filled as commands arrive:

```csharp
public static IServiceCollection AddApplicationModule(this IServiceCollection services)
{
    return services;
}
```

## Tests

`{Command}HandlerTests` for every handler that writes; `{EventHandler}Tests` for every event handler. Snippets in `worked-slice.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Application/ApplicationModule.cs` | `{Name}.Application` | FluentValidation, Microsoft.Extensions.DependencyInjection, {Name}.Application.Customers, {Name}.Application.Orders, {Name}.Application.Products, {Name}.Domain.Aggregates.Orders.Events, {Name}.Shared.Application, {Name}.Shared.Domain |
