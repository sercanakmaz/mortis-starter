# The worked slice: the example domain, C#

Generated when `example` is `true`. Its tests are in `worked-slice-tests.md`. The domain is specified in roles in `mortis-architect/references/architecture/example-domain.md`; this page is its C# rendering, member by member. It is also the slice every new aggregate imitates. File paths are in `manifest.json`; names follow `naming.md`.

## The order of work

Event handlers of products and customers react to order events, so the domain comes first for all three, then the rest. Build after each line:

1. Domain of Category, then Product, then Customer, then Order (constants, value objects, child entities, the root, events, repository interface), with each one's test support (`Fake{Aggregate}`, `{Aggregate}RepositoryBuilder`) and domain tests.
2. Application of Category, then Order, then Customer, then Product (view, commands, event handlers and consumers, registration lines), each followed by its handler, event handler and consumer tests.
3. Infrastructure of the four (configuration, repository, registration line) and their tests.
4. The four controllers.

Then the migration (`infrastructure-postgres.md` "Migrations"). The Console needs nothing for the example: it finds every command by name.

## Categories

The one aggregate of the example with a key the database assigns (`int`). Its id is unknown until the insert, so its constructor raises nothing; the handler creates it, then calls `MarkCreated`, which raises `Created` with the real id, then hands the event over with `UpdateAsync`.

### Domain

@@members src/Boilerplate.Domain/Aggregates/Categories/CategoryConstants.cs::CategoryConstants@@

@@members src/Boilerplate.Domain/Aggregates/Categories/Category.cs::Category@@

@@decl src/Boilerplate.Domain/Aggregates/Categories/Events/Created.cs::Created@@

@@decl src/Boilerplate.Domain/Aggregates/Categories/ICategoryRepository.cs::ICategoryRepository@@

### Application

@@members src/Boilerplate.Application/Categories/CategoryDto.cs::CategoryDto@@

@@members src/Boilerplate.Application/Categories/CreateCategoryCommand.cs::CreateCategoryCommand@@

@@member src/Boilerplate.Application/Categories/CreateCategoryCommand.cs::CreateCategoryCommandValidator::CreateCategoryCommandValidator@@

@@decl src/Boilerplate.Application/Categories/CreateCategoryCommand.cs::CreateCategoryCommandHandler@@

@@member src/Boilerplate.Application/Categories/CreateCategoryCommand.cs::CreateCategoryCommandHandler::OnHandle@@

@@decl src/Boilerplate.Application/Categories/GetCategoryCommand.cs::GetCategoryCommand@@

@@member src/Boilerplate.Application/Categories/GetCategoryCommand.cs::GetCategoryCommandHandler::OnHandle@@

## Products

### Domain

@@members src/Boilerplate.Domain/Aggregates/Products/ProductConstants.cs::ProductConstants@@

@@members src/Boilerplate.Domain/Aggregates/Products/Product.cs::Product@@

Events, one file each in `Events/`:

@@decl src/Boilerplate.Domain/Aggregates/Products/Events/Created.cs::Created@@

@@decl src/Boilerplate.Domain/Aggregates/Products/Events/StockIncreased.cs::StockIncreased@@

@@decl src/Boilerplate.Domain/Aggregates/Products/Events/StockDecreased.cs::StockDecreased@@

```csharp
public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product> FindBySkuTrackingAsync(string sku);
}
```

### Application

@@members src/Boilerplate.Application/Products/ProductDto.cs::ProductDto@@

| Command | Properties | Validator | Handler loads | Calls | Saves |
|---|---|---|---|---|---|
| `CreateProductCommand` | `Id` (default `Guid.CreateVersion7()`), `Sku`, `Name`, `Stock`, `int? CategoryId` | id not empty; sku, name required within their lengths; stock `>= 0`; category id `> 0` when given | none | `new Product(command.Id, command.Sku, command.Name, command.Stock, command.Context.User, command.CategoryId)` | `CreateAsync` |
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
    product.SetUpdatedAudit(command.Context.User);

    await productRepository.UpdateAsync(product);

    return CommandResult.FromContent(ProductDto.Map(product));
}
```

@@member src/Boilerplate.Application/Products/DecreaseProductStockCommand.cs::DecreaseProductStockCommandValidator::DecreaseProductStockCommandValidator@@

Event consumers (after the commit, as the system user), in `Products/`; each gets its `AddEventConsumer` line in the hosts that run it (`event-consumers.md`):

@@member src/Boilerplate.Application/Products/DecreaseStockWhenOrderLineAddedEventConsumer.cs::DecreaseStockWhenOrderLineAddedEventConsumer::OnHandle@@

@@member src/Boilerplate.Application/Products/IncreaseStockWhenOrderLineCancelledEventConsumer.cs::IncreaseStockWhenOrderLineCancelledEventConsumer::OnHandle@@

## Customers

### Domain

@@members src/Boilerplate.Domain/Aggregates/Customers/CustomerConstants.cs::CustomerConstants@@

@@members src/Boilerplate.Domain/Aggregates/Customers/Customer.cs::Customer@@

@@decl src/Boilerplate.Domain/Aggregates/Customers/Events/Created.cs::Created@@

@@decl src/Boilerplate.Domain/Aggregates/Customers/Events/Blocked.cs::Blocked@@

```csharp
public interface ICustomerRepository : IRepository<Customer, Guid>;
```

### Application

@@members src/Boilerplate.Application/Customers/CustomerDto.cs::CustomerDto@@

| Command | Properties | Validator | Handler loads | Calls | Saves |
|---|---|---|---|---|---|
| `CreateCustomerCommand` | `Id` (default `Guid.CreateVersion7()`), `Name`, `Email` | id not empty; name required within length; email required, well formed, within length | none | `new Customer(command.Id, command.Name, command.Email, command.Context.User)` | `CreateAsync` |
| `GetCustomerCommand(Guid Id)` | | none | `FindByIdAsync` | | |
| `BlockCustomerCommand(Guid Id)` | | none | `FindByIdTrackingAsync` | `Block()` | `UpdateAsync` |
| `RegisterCompletedOrderCommand(Guid Id)` | | none | `FindByIdTrackingAsync` | `RegisterCompletedOrder()` | `UpdateAsync` |

@@member src/Boilerplate.Application/Customers/CreateCustomerCommand.cs::CreateCustomerCommandValidator::CreateCustomerCommandValidator@@

Event handler (sync, as the user of the event), in `Customers/`. The event is the order's `Completed`:

@@member src/Boilerplate.Application/Customers/RegisterCompletedOrderWhenOrderCompletedEventHandler.cs::RegisterCompletedOrderWhenOrderCompletedEventHandler::OnHandle@@

## Orders

### Domain

@@members src/Boilerplate.Domain/Aggregates/Orders/OrderConstants.cs::OrderConstants@@

@@data src/Boilerplate.Domain/Aggregates/Orders/OrderStatus.cs csharp@@

@@members src/Boilerplate.Domain/Aggregates/Orders/Money.cs::Money@@

@@members src/Boilerplate.Domain/Aggregates/Orders/OrderLine.cs::OrderLine@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/OrderLineModel.cs::OrderLineModel@@

@@members src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/Events/Created.cs::Created@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/Events/LineAdded.cs::LineAdded@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/Events/LineCancelled.cs::LineCancelled@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/Events/Completed.cs::Completed@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/Events/Cancelled.cs::Cancelled@@

```csharp
public interface IOrderRepository : IRepository<Order, Guid>;
```

### Application

@@members src/Boilerplate.Application/Orders/OrderDto.cs::OrderDto@@

@@members src/Boilerplate.Application/Orders/OrderDto.cs::OrderLineDto@@

| Command | Properties | Handler loads | Calls | Saves |
|---|---|---|---|---|
| `CreateOrderCommand` | `Id` (default), `OrderNumber`, `CustomerId`, `List<OrderLineModel> Lines` | none | `new Order(...)`, then `AddLine` per line | `CreateAsync` |
| `GetOrderCommand(Guid Id)` | | `FindByIdAsync` | | |
| `AddOrderLineCommand` | `Id`, `OrderLineModel Line` | `FindByIdTrackingAsync` | `AddLine(command.Line.Sku, new Money(command.Line.Price, command.Line.Currency), command.Line.Quantity)` | `UpdateAsync` |
| `CancelOrderLineCommand` | `Id`, `LineId`, `Reason` | `FindByIdTrackingAsync` | `CancelLine(command.LineId, command.Reason)` | `UpdateAsync` |
| `CompleteOrderCommand(Guid Id)` | | `FindByIdTrackingAsync` | `Complete()` | `UpdateAsync` |
| `CancelOrderWhenAllLinesCancelledCommand(Guid Id)` | | `FindByIdTrackingAsync` | `CancelWhenAllLinesCancelled()` | `UpdateAsync` only when it returned `true` |

The handlers of `CreateOrderCommand`, `CompleteOrderCommand`, `GetOrderCommand` and `CancelOrderWhenAllLinesCancelledCommand` are written out in `application.md`.

@@member src/Boilerplate.Application/Orders/CreateOrderCommand.cs::CreateOrderCommandValidator::CreateOrderCommandValidator@@

@@member src/Boilerplate.Application/Orders/AddOrderLineCommand.cs::AddOrderLineCommandValidator::AddOrderLineCommandValidator@@

@@member src/Boilerplate.Application/Orders/CancelOrderLineCommand.cs::CancelOrderLineCommandValidator::CancelOrderLineCommandValidator@@

Event handler (sync, as the user of the event), in `Orders/`:

@@member src/Boilerplate.Application/Orders/CancelOrderWhenOrderLineCancelledEventHandler.cs::CancelOrderWhenOrderLineCancelledEventHandler::OnHandle@@

### Registration

@@member src/Boilerplate.Application/ApplicationModule.cs::ApplicationModule::AddApplicationModule@@

## Infrastructure

@@member src/Boilerplate.Infrastructure/Persistence/Categories/CategoryConfiguration.cs::CategoryConfiguration::Configure@@

@@member src/Boilerplate.Infrastructure/Persistence/Products/ProductConfiguration.cs::ProductConfiguration::Configure@@

@@member src/Boilerplate.Infrastructure/Persistence/Customers/CustomerConfiguration.cs::CustomerConfiguration::Configure@@

The order and order-line configurations are in `infrastructure-postgres.md`.

`CategoryRepository` (`EfRepository<Category, int>`), `CustomerRepository` and `OrderRepository` add nothing to `EfRepository`. `ProductRepository` adds `FindBySkuTrackingAsync` (`infrastructure-postgres.md`).

```csharp
public static IServiceCollection AddPersistenceModule(this IServiceCollection services)
{
    services.AddScoped<ICategoryRepository, CategoryRepository>();
    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();

    return services;
}
```

## Controllers

@@members src/Boilerplate.Api/Controllers/CategoriesController.cs::CategoriesController@@

@@members src/Boilerplate.Api/Controllers/ProductsController.cs::ProductsController@@

@@members src/Boilerplate.Api/Controllers/CustomersController.cs::CustomersController@@

@@members src/Boilerplate.Api/Controllers/OrdersController.cs::OrdersController@@

## Trying it from the Console

A flow is a sequence of runs; each prints the result on stdout:

```bash
dotnet run --project src/{Name}.Console -- CreateProduct '{"sku":"BOLT","name":"Bolt","stock":10}'
dotnet run --project src/{Name}.Console -- CreateCustomer '{"name":"Ada","email":"ada@example.com"}'
dotnet run --project src/{Name}.Console -- CreateOrder '{"orderNumber":"ORD-1","customerId":"<id>","lines":[{"sku":"BOLT","price":1,"currency":"EUR","quantity":3}]}'
dotnet run --project src/{Name}.Console -- GetProduct '{"id":"<id>"}'
```

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.


A handler written to `application.md`'s shapes also needs `{Name}.Shared.Exceptions` (for `NotFoundException`); the handler and event handler tests need `Moq`, `{Name}.Shared.Exceptions`, `{Name}.Shared.Persistence` and the `TestSupport` namespaces of the builders they use. The build names any that is missing.

@@usings worked-slice.md@@
