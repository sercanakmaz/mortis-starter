# Read models, C#

Applies when `queries` is `read-models` (it includes everything on `queries.md`). Project `{Name}.Infrastructure`, folder `ReadModels/`; the query and its reader port as on `queries.md`. Layer 1: `options/queries.md`, `architecture/queries.md`.

**Built** in the advanced reference solution (with tenancy), decision 52: compiled, tested and verified there. The snippets below are its code.

A read model is a table shaped for one screen. Infrastructure owns it, sync event handlers keep it in step with the write in the same transaction, and a Console command rebuilds it from the aggregates. The example's is `OrderSummary`: order number, customer name, status, total and line count, for an order list screen.

## How it fits together

```
command commits an order change
  SaveChanges -> sync handlers run inside the transaction:
    OrderSummaryRefresh<Created | LineAdded | LineCancelled | Completed | Cancelled>
      -> OrderSummaryProjection.RefreshAsync(orderId): one upsert computed from orders, order_lines, customers
  COMMIT: the order and its summary change together, or neither does
GET /orders/summaries -> ListOrderSummariesQuery -> IOrderSummaryReader -> select from order_summaries
dotnet run -- rebuild-order-summary -> delete every summary, recompute them all, in one transaction
```

- A read-model handler writes only its read model; it never changes an aggregate or sends a command. That is the one exception to "an event handler sends a command" (rule 19), and these handlers live in Infrastructure for that reason.
- The projection recomputes the whole row from the aggregate's tables instead of applying each event as a delta, so a missed or repeated event cannot leave it wrong, and the rebuild is the same statement for every order.
- The handlers run last among the sync handlers (`Sequence` 100), after the handlers that change the order further.
- A customer renamed after the order keeps the old name in the summary until the next order change or a rebuild; a `Renamed` event handler on the customer would refresh that customer's summaries.
- With tenancy, the summary copies `tenant_id` from the order and implements `IMultiTenant`, so the query filter applies to it; the projection's raw SQL is outside the filter and keeps the tenant by copying it.
- Eventually consistent read models (updated by event consumers after the commit) need an outbox (rule R10); the example uses the consistent kind.

## OrderSummary

File `src/{Name}.Infrastructure/ReadModels/OrderSummaries/OrderSummary.cs`, a plain class (not an aggregate), and its configuration in the same folder:

```csharp
public class OrderSummary : IMultiTenant
{
    public Guid OrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public string OrderNumber { get; private set; }
    public string CustomerName { get; private set; }
    public string Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int LineCount { get; private set; }
}
```

```csharp
public class OrderSummaryConfiguration : IEntityTypeConfiguration<OrderSummary>
{
    public void Configure(EntityTypeBuilder<OrderSummary> builder)
    {
        builder.ToTable("order_summaries");
        builder.HasKey(orderSummary => orderSummary.OrderId);
        builder.Property(orderSummary => orderSummary.OrderNumber).HasMaxLength(OrderConstants.OrderNumberMaxLength).IsRequired();
        builder.Property(orderSummary => orderSummary.CustomerName).HasMaxLength(CustomerConstants.NameMaxLength).IsRequired();
        builder.Property(orderSummary => orderSummary.Status).HasMaxLength(OrderSummaryProjection.StatusMaxLength).IsRequired();
        builder.Property(orderSummary => orderSummary.TotalAmount).HasPrecision(18, 4);
        builder.HasIndex(orderSummary => new { orderSummary.TenantId, orderSummary.Status });
    }
}
```

The table is part of the project's `Initial` migration when the project is generated; a project that adds a read model later gets it from its next migration.

## OrderSummaryProjection

File `ReadModels/OrderSummaries/OrderSummaryProjection.cs`. One statement refreshes one order or, for the rebuild, all of them:

```csharp
public class OrderSummaryProjection(AppDbContext context)
{
    public const int StatusMaxLength = 32;

    public Task RefreshAsync(Guid orderId)
    {
        return context.Database.ExecuteSqlRawAsync(Upsert("where o.id = {0}"), orderId);
    }

    public async Task RebuildAsync()
    {
        await context.Database.ExecuteSqlRawAsync("delete from order_summaries");
        await context.Database.ExecuteSqlRawAsync(Upsert(""));
    }

    private static string Upsert(string condition)
    {
        return $"""
            insert into order_summaries (order_id, tenant_id, order_number, customer_name, status, total_amount, line_count)
            select o.id, o.tenant_id, o.order_number, c.name, o.status,
                   coalesce(sum(l.price_amount * l.quantity) filter (where not l.is_cancelled), 0),
                   count(l.id) filter (where not l.is_cancelled)
            from orders o
            join customers c on c.id = o.customer_id
            left join order_lines l on l.order_id = o.id
            {condition}
            group by o.id, c.name
            on conflict (order_id) do update set
                tenant_id = excluded.tenant_id, order_number = excluded.order_number, customer_name = excluded.customer_name, status = excluded.status,
                total_amount = excluded.total_amount, line_count = excluded.line_count
            """;
    }
}
```

- It runs on the context's connection and transaction, after `SaveChanges`, so it sees the command's own writes.
- The condition is a fixed string, never caller input; the order id is a parameter. One statement with `{0} is null or o.id = {0}` would not work: PostgreSQL cannot type a null parameter.
- The snippets show tenancy (the example's read model lives in the advanced preset). Without tenancy, drop `TenantId`, `IMultiTenant`, the `tenant_id` column and its place in the index.

## The handlers

File `ReadModels/OrderSummaries/OrderSummaryRefresh.cs`, one generic handler for every order event:

```csharp
public class OrderSummaryRefresh<TEvent>(OrderSummaryProjection orderSummaryProjection, Func<TEvent, string> orderIdOf) : BaseSyncEventHandler<TEvent> where TEvent : EventBase
{
    public override int Sequence => 100;

    protected override Task OnHandle(TEvent @event)
    {
        return orderSummaryProjection.RefreshAsync(Guid.Parse(orderIdOf(@event)));
    }
}
```

File `ReadModels/ReadModelModule.cs`, called by `AddInfrastructureModule`:

```csharp
public static class ReadModelModule
{
    public static IServiceCollection AddReadModelModule(this IServiceCollection services)
    {
        services.AddScoped<OrderSummaryProjection>();
        services.AddScoped<IOrderSummaryReader, OrderSummaryReader>();
        services.AddOrderSummaryRefresh<Created>(@event => @event.Id);
        services.AddOrderSummaryRefresh<LineAdded>(@event => @event.Id);
        services.AddOrderSummaryRefresh<LineCancelled>(@event => @event.Id);
        services.AddOrderSummaryRefresh<Completed>(@event => @event.Id);
        services.AddOrderSummaryRefresh<Cancelled>(@event => @event.Id);

        return services;
    }

    private static void AddOrderSummaryRefresh<TEvent>(this IServiceCollection services, Func<TEvent, string> orderIdOf) where TEvent : EventBase
    {
        services.AddScoped<BaseSyncEventHandler<TEvent>>(serviceProvider => new OrderSummaryRefresh<TEvent>(serviceProvider.GetRequiredService<OrderSummaryProjection>(), orderIdOf));
    }
}
```

## Reading it

`IOrderSummaryReader` and `OrderSummaryView` in `src/{Name}.Domain/Aggregates/Orders/`:

```csharp
public interface IOrderSummaryReader
{
    Task<PagedResult<OrderSummaryView>> ListAsync(int page, int size, string status);
}
```

```csharp
public record OrderSummaryView(Guid OrderId, string OrderNumber, string CustomerName, string Status, decimal TotalAmount, int LineCount);
```

File `ReadModels/OrderSummaries/OrderSummaryReader.cs`:

```csharp
public class OrderSummaryReader(AppDbContext context) : IOrderSummaryReader
{
    public async Task<PagedResult<OrderSummaryView>> ListAsync(int page, int size, string status)
    {
        var summaries = context.Set<OrderSummary>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            summaries = summaries.Where(orderSummary => orderSummary.Status == status);
        }

        var total = await summaries.CountAsync();
        var items = await summaries
            .OrderBy(orderSummary => orderSummary.OrderNumber).ThenBy(orderSummary => orderSummary.OrderId)
            .Skip(PageRequest.Skip(page, size)).Take(size)
            .Select(orderSummary => new OrderSummaryView(orderSummary.OrderId, orderSummary.OrderNumber, orderSummary.CustomerName, orderSummary.Status, orderSummary.TotalAmount, orderSummary.LineCount))
            .ToListAsync();

        return new PagedResult<OrderSummaryView>(items, page, size, total);
    }
}
```

File `src/{Name}.Application/Orders/ListOrderSummariesQuery.cs`; the view is the answer as it is (a read model is already the screen's shape):

```csharp
public record ListOrderSummariesQuery : IQuery<PagedResult<OrderSummaryView>>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public int Page { get; set; } = 1;
    public int Size { get; set; } = PageRequest.DefaultSize;
    public string Status { get; set; }
}

public class ListOrderSummariesQueryValidator : AbstractValidator<ListOrderSummariesQuery>
{
    public ListOrderSummariesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.Size).InclusiveBetween(1, PageRequest.MaxSize);
        RuleFor(query => query.Status).IsEnumName(typeof(OrderStatus)).When(query => query.Status is not null);
    }
}

public class ListOrderSummariesQueryHandler(IOrderSummaryReader orderSummaryReader, IEnumerable<IValidator<ListOrderSummariesQuery>> validators) : BaseQueryHandler<ListOrderSummariesQuery, PagedResult<OrderSummaryView>>(validators)
{
    protected override Task<PagedResult<OrderSummaryView>> OnHandle(ListOrderSummariesQuery query)
    {
        return orderSummaryReader.ListAsync(query.Page, query.Size, query.Status);
    }
}
```

`ApplicationModule` registers it with its validator; `AddInfrastructureModule` calls `services.AddReadModelModule();`. `OrdersController`, before the `{id}` route:

```csharp
    [HttpGet("summaries")]
    public async Task<IActionResult> ListSummaries([FromQuery] ListOrderSummariesQuery query)
    {
        return Ok(await queryDispatcher.Ask(query));
    }
```

## Rebuilding

`ConsoleConstants` gains `public const string RebuildOrderSummaryCommand = "rebuild-order-summary";`. A run still names its user (`--user`), so `Program.cs` handles the name after `ConsoleArguments.Parse` and entering the tenant, in one transaction that crosses tenants:

```csharp
    if (consoleArguments.Name == ConsoleConstants.RebuildOrderSummaryCommand)
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        using (scope.ServiceProvider.GetRequiredService<TenantScope>().Bypass(ConsoleConstants.RebuildOrderSummaryCommand))
        {
            await unitOfWork.BeginTransactionAsync(ConsoleConstants.RebuildOrderSummaryCommand);
            await scope.ServiceProvider.GetRequiredService<OrderSummaryProjection>().RebuildAsync();
            await unitOfWork.CommitTransactionAsync(ConsoleConstants.RebuildOrderSummaryCommand);
        }

        return 0;
    }
```

Without tenancy there is no `TenantScope` and no `using` block.

## Tests

The projection is SQL, so it is proven against PostgreSQL by `scripts/conformance/queries.sh`: after the example's flows every summary equals its order, and the rebuild recreates them from an empty table. One unit test pins the registrations, `test/{Name}.Tests/Infrastructure/ReadModels/ReadModelModuleTests.cs`:

```csharp
public class ReadModelModuleTests
{
    [Fact]
    public void AddReadModelModule_Should_RefreshTheSummaryOnEveryOrderEvent()
    {
        var services = new ServiceCollection().AddReadModelModule();

        Assert.All([typeof(Created), typeof(LineAdded), typeof(LineCancelled), typeof(Completed), typeof(Cancelled)],
            eventType => Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(BaseSyncEventHandler<>).MakeGenericType(eventType)));
    }
}
```

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `OrderSummary.cs`, `OrderSummaryConfiguration.cs` | `{Name}.Infrastructure.ReadModels.OrderSummaries` | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Metadata.Builders`, `{Name}.Domain.Aggregates.Customers`, `{Name}.Domain.Aggregates.Orders` |
| `OrderSummaryProjection.cs` | same | `Microsoft.EntityFrameworkCore`, `{Name}.Infrastructure.Persistence` |
| `OrderSummaryRefresh.cs` | same | `{Name}.Shared.Domain` |
| `ReadModelModule.cs` | `{Name}.Infrastructure.ReadModels` | `Microsoft.Extensions.DependencyInjection`, `{Name}.Domain.Aggregates.Orders`, `{Name}.Domain.Aggregates.Orders.Events`, `{Name}.Infrastructure.ReadModels.OrderSummaries`, `{Name}.Shared.Domain` |

## Decision record

`docs/adr/0018-queries-and-read-models.md`, in `project-docs-adr.md`.
