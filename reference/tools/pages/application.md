# Application: commands, handlers, views, event handlers and consumers, C#

Project `{Name}.Application`. References Domain and Shared. Layer 1: `layers.md` "Application", `building-blocks.md` Command, Validator, Command handler, DTO, Sync and Event consumer; the decision cheat sheet in `placement-guide.md`.

## The command file

One file per command, `{Plural}/{Action}{Aggregate}Command.cs`, holding three types in this order: the command, its validator (if it has rules), its handler.

### The command

A `record` with init-able public properties (`{ get; set; }`), so both JSON and `with` work.

@@decl src/Boilerplate.Application/Orders/CreateOrderCommand.cs::CreateOrderCommand@@

@@member src/Boilerplate.Application/Orders/CreateOrderCommand.cs::CreateOrderCommand::Context@@

@@member src/Boilerplate.Application/Orders/CreateOrderCommand.cs::CreateOrderCommand::Id@@

@@member src/Boilerplate.Application/Orders/CreateOrderCommand.cs::CreateOrderCommand::Lines@@

- A command that writes implements `ICommand<TView>, IContextCommand, ITransactionalCommand` and declares one line, `Context`, with `[JsonIgnore]`; never a user, correlation or tenant property of its own (`using System.Text.Json.Serialization;`).
- A create command carries its new id with a default of `Guid.CreateVersion7()`, so the client may send one.
- A command with only an id is positional, and still declares its context in its body:

@@decl src/Boilerplate.Application/Orders/CompleteOrderCommand.cs::CompleteOrderCommand@@

@@member src/Boilerplate.Application/Orders/CompleteOrderCommand.cs::CompleteOrderCommand::Context@@

- A read is `Get{Aggregate}Command(Guid Id) : ICommand<{Aggregate}Dto>`: no user, no transaction.

### The validator

`AbstractValidator<TCommand>` (`using FluentValidation;`), checking only what the command alone shows: required, format, length (from `{Aggregate}Constants`), range.

@@member src/Boilerplate.Application/Orders/CreateOrderCommand.cs::CreateOrderCommandValidator::CreateOrderCommandValidator@@

A nested object that may be missing:

@@member src/Boilerplate.Application/Orders/AddOrderLineCommand.cs::AddOrderLineCommandValidator::AddOrderLineCommandValidator@@

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
    order.SetUpdatedAudit(command.Context.User);

    await orderRepository.UpdateAsync(order);

    return CommandResult.FromContent(OrderDto.Map(order));
}
```

**Create:**

```csharp
protected override async Task<CommandResult<OrderDto>> OnHandle(CreateOrderCommand command)
{
    var order = new Order(command.Id, command.OrderNumber, command.CustomerId, command.Context.User);

    foreach (var line in command.Lines)
    {
        order.AddLine(line.Sku, new Money(line.Price, line.Currency), line.Quantity);
    }

    await orderRepository.CreateAsync(order);

    return CommandResult.FromContent(OrderDto.Map(order));
}
```

**Create an aggregate whose id the database assigns:** create, mark created, hand the event over:

@@member src/Boilerplate.Application/Categories/CreateCategoryCommand.cs::CreateCategoryCommandHandler::OnHandle@@

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
        order.SetUpdatedAudit(command.Context.User);

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
    source.SetUpdatedAudit(command.Context.User);
    target.SetUpdatedAudit(command.Context.User);

    await warehouseRepository.UpdateAsync(source);
    await warehouseRepository.UpdateAsync(target);

    return CommandResult.FromContent(TransferDto.Map(source, target));
}
```

The transfer is an illustration; the example domain has no command of this shape.

- The user is `command.Context.User`; the rest of the context reaches the events without the handler doing anything.
- `SetUpdatedAudit` comes after the operation, so the operation's events carry the user.
- The handler never catches a business failure and never decides with an `if` of its own; the one `if` allowed is on an operation's own "did it change" answer.
- A consequence in another aggregate is an event handler, never another line here.

## The view

File `{Plural}/{Aggregate}Dto.cs`. A record with settable properties and a static `Map` that names every field it reads. A child view sits in the same file.

@@member src/Boilerplate.Application/Orders/OrderDto.cs::OrderDto::Map@@

@@member src/Boilerplate.Application/Orders/OrderDto.cs::OrderLineDto::Map@@

- An enum is mapped with `.ToString()`. No mapping library.

## Event handlers

File `{ReactingPlural}/{Action}When{Aggregate}{Event}EventHandler.cs`, in the folder of the aggregate that reacts. Body: one awaited `Send`. The event's own namespace is imported with an alias when another aggregate has an event of the same name.

The command it sends gets its context from `ContextOf(@event)`, which the base class gives: the same correlation id, the event as its cause, and the event's user, the user whose command raised it, for a sync handler and an event consumer alike. A sync handler:

```csharp
public class CancelOrderWhenOrderLineCancelledEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<LineCancelled>
```

@@member src/Boilerplate.Application/Orders/CancelOrderWhenOrderLineCancelledEventHandler.cs::CancelOrderWhenOrderLineCancelledEventHandler::OnHandle@@

An event consumer is the same with `BaseEventConsumer<TEvent>`, named `{Action}When{Aggregate}{Event}EventConsumer.cs`; its command runs as the event's user too:

```csharp
public class DecreaseStockWhenOrderLineAddedEventConsumer(ICommandDispatcher commandDispatcher) : BaseEventConsumer<LineAdded>
```

@@member src/Boilerplate.Application/Products/DecreaseStockWhenOrderLineAddedEventConsumer.cs::DecreaseStockWhenOrderLineAddedEventConsumer::OnHandle@@

A consumer is not registered here: it gets one `AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(10))` line in the host that runs it (`event-consumers.md`).

## Registration

File `ApplicationModule.cs`, namespace `{Name}.Application`. Grouped by aggregate; inside a group, per command: the handler, then its validator; then the group's sync event handlers. Event consumers are not here (`event-consumers.md`). Needs `using FluentValidation;`, `using {Name}.Shared.Application;` and `using {Name}.Shared.Domain;`.

@@decl src/Boilerplate.Application/ApplicationModule.cs::ApplicationModule@@

```csharp
services.AddCommandHandler<CreateOrderCommand, OrderDto, CreateOrderCommandHandler>();
services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
services.AddScoped<BaseSyncEventHandler<LineCancelled>, CancelOrderWhenOrderLineCancelledEventHandler>();
```

Written first with an empty body, and filled as commands arrive:

```csharp
public static IServiceCollection AddApplicationModule(this IServiceCollection services)
{
    return services;
}
```

## Tests

`{Command}HandlerTests` for every handler that writes; `{EventHandler}Tests` for every sync event handler and `{EventConsumer}Tests` for every event consumer. Snippets in `worked-slice.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings application.md@@
