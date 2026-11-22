# Queries, C#

Applies when `queries` is `split` or `read-models`. Projects `{Name}.Shared` (folder `Application/`), `{Name}.Domain` (the reader ports), `{Name}.Application`, `{Name}.Infrastructure` (folder `Persistence/`) and the hosts. Layer 1: `options/queries.md`, `architecture/queries.md`. Read models are on `read-models.md`.

**Not built or run yet** (decision 46): every snippet on this page is written by hand against the reference solutions' code; phase 2 compiles and proves it. `docs/unverified.md` lists it.

With `queries` = `commands` (the core), a read is a `Get*Command` that loads the aggregate and maps it (`application.md`); nothing on this page is generated.

## How it fits together

```
host     GET /products?page=1&size=20&sort=NameAscending&nameContains=bolt
           -> ListProductsQuery, its Context filled by the host like a command's
         queryDispatcher.Ask(query) -> PagedResult<ProductDto>
handler  BaseQueryHandler: validate -> OnHandle
         reads through a port declared in Domain next to the repository (IProductReader), never a repository
reader   Infrastructure: no tracking, a projection straight to a view record; filters, sort and page in SQL
         the handler maps the view to the DTO
```

- A query has no transaction, raises no event and changes nothing. It may have a validator.
- A query handler never depends on `IUnitOfWork`, a repository or a domain service; an architecture test checks it.
- A reader projects into a view record in the database (`Select`), so it never loads an aggregate to map it.
- The reader port and its view records are in Domain, next to `I{Aggregate}Repository`, because Infrastructure references only Domain and Shared (`architecture/layers.md`); the plan's port in Application would have made Infrastructure reference Application. The handler maps the view to the Application DTO.
- The tenant filter and the caller's language apply as they do to commands: the host enters the tenant scope and fills the context before the query runs.
- Queries are not wrapped in the command pipeline, so they write no `started`/`completed` log lines of their own.
- Filtering is typed: each list query has its own fields and a closed `Sort` enum. There is no generic filter language.
- The example's `Get*Command`s become `Get*Query`s; `ListProductsQuery` is new.

## Shared: the query types

File `src/{Name}.Shared/Application/IQuery.cs`. The non-generic interface lets the host find queries among an action's arguments:

```csharp
public interface IQuery
{
    CommandContext Context { get; set; }
}

public interface IQuery<TResult> : IQuery;
```

File `BaseQueryHandler.cs`:

```csharp
public abstract class BaseQueryHandler<TQuery, TResult>(IEnumerable<IValidator<TQuery>> validators) where TQuery : IQuery<TResult>
{
    public async Task<TResult> Handle(TQuery query)
    {
        query.Context ??= new CommandContext();

        var validationFailures = validators.SelectMany(validator => validator.Validate(query).Errors).ToList();

        if (validationFailures.Count > 0)
        {
            throw new ValidationFailedException(validationFailures
                .GroupBy(validationFailure => validationFailure.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(validationFailure => validationFailure.ErrorMessage).ToArray()));
        }

        return await OnHandle(query);
    }

    protected abstract Task<TResult> OnHandle(TQuery query);
}
```

`ValidationFailedException` takes the failures grouped by property, exactly as `CommandPipeline` passes them (`shared-application.md`).

File `IQueryDispatcher.cs` and `QueryDispatcher.cs`, the same shape as `CommandDispatcher`:

```csharp
public interface IQueryDispatcher
{
    Task<TResult> Ask<TResult>(IQuery<TResult> query);
}
```

```csharp
public sealed class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> QueryHandlerInvokers = new();

    public Task<TResult> Ask<TResult>(IQuery<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryHandlerInvoker = (QueryHandlerInvoker<TResult>)QueryHandlerInvokers.GetOrAdd(
            query.GetType(),
            queryType => Activator.CreateInstance(typeof(QueryHandlerInvoker<,>).MakeGenericType(queryType, typeof(TResult))));

        return queryHandlerInvoker.Invoke(serviceProvider, query);
    }
}

public abstract class QueryHandlerInvoker<TResult>
{
    public abstract Task<TResult> Invoke(IServiceProvider serviceProvider, IQuery<TResult> query);
}

public sealed class QueryHandlerInvoker<TQuery, TResult> : QueryHandlerInvoker<TResult> where TQuery : IQuery<TResult>
{
    public override Task<TResult> Invoke(IServiceProvider serviceProvider, IQuery<TResult> query)
    {
        return serviceProvider.GetRequiredService<BaseQueryHandler<TQuery, TResult>>().Handle((TQuery)query);
    }
}
```

File `QueryHandlerExtensions.cs`; `AddSharedModule` registers the dispatcher (`services.AddScoped<IQueryDispatcher, QueryDispatcher>();`):

```csharp
public static class QueryHandlerExtensions
{
    public static IServiceCollection AddQueryHandler<TQuery, TResult, THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where THandler : BaseQueryHandler<TQuery, TResult>
    {
        services.AddScoped<BaseQueryHandler<TQuery, TResult>, THandler>();

        return services;
    }
}
```

Files `PageRequest.cs` and `PagedResult.cs`:

```csharp
public static class PageRequest
{
    public const int DefaultSize = 20;
    public const int MaxSize = 100;

    public static int Skip(int page, int size) => (page - 1) * size;
}
```

```csharp
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int Size, int Total);
```

## Domain: the reader port and its views

File `src/{Name}.Domain/Aggregates/Products/IProductReader.cs`, with the view record, the sort set and the list filter it takes:

```csharp
public interface IProductReader
{
    Task<ProductView> FindAsync(Guid id);
    Task<PagedResult<ProductView>> ListAsync(ProductListFilter filter);
}
```

File `ProductView.cs`:

```csharp
public record ProductView(Guid Id, string Sku, string Name, int Stock, int? CategoryId);

public enum ProductSort
{
    SkuAscending,
    NameAscending,
    StockDescending
}

public record ProductListFilter(int Page, int Size, ProductSort Sort, string NameContains, int? CategoryId);
```

The view is a read shape, not an aggregate: no behaviour, no audit fields, nothing the reader does not select. The folder pattern `*View.cs` joins `*Model.cs` in `aggregate_files`.

## Application: the queries and their handlers

`ProductDto` gains `public static ProductDto From(ProductView view)`, mapping the same fields.

File `src/{Name}.Application/Products/GetProductQuery.cs`, which replaces `GetProductCommand.cs`:

```csharp
public record GetProductQuery : IQuery<ProductDto>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; }
}

public class GetProductQueryHandler(IProductReader productReader, IEnumerable<IValidator<GetProductQuery>> validators) : BaseQueryHandler<GetProductQuery, ProductDto>(validators)
{
    protected override async Task<ProductDto> OnHandle(GetProductQuery query)
    {
        var productView = await productReader.FindAsync(query.Id) ?? throw new NotFoundException(nameof(Product));

        return ProductDto.From(productView);
    }
}
```

File `ListProductsQuery.cs`, with its validator and its handler:

```csharp
public record ListProductsQuery : IQuery<PagedResult<ProductDto>>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public int Page { get; set; } = 1;
    public int Size { get; set; } = PageRequest.DefaultSize;
    public ProductSort Sort { get; set; }
    public string NameContains { get; set; }
    public int? CategoryId { get; set; }
}

public class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.Size).InclusiveBetween(1, PageRequest.MaxSize);
        RuleFor(query => query.Sort).IsInEnum();
        RuleFor(query => query.NameContains).MaximumLength(ProductConstants.NameMaxLength);
    }
}

public class ListProductsQueryHandler(IProductReader productReader, IEnumerable<IValidator<ListProductsQuery>> validators) : BaseQueryHandler<ListProductsQuery, PagedResult<ProductDto>>(validators)
{
    protected override async Task<PagedResult<ProductDto>> OnHandle(ListProductsQuery query)
    {
        var page = await productReader.ListAsync(new ProductListFilter(query.Page, query.Size, query.Sort, query.NameContains, query.CategoryId));

        return new PagedResult<ProductDto>(page.Items.Select(ProductDto.From).ToList(), page.Page, page.Size, page.Total);
    }
}
```

`ApplicationModule` registers each query in place of its `Get*Command`:

```csharp
        services.AddQueryHandler<GetProductQuery, ProductDto, GetProductQueryHandler>();
        services.AddQueryHandler<ListProductsQuery, PagedResult<ProductDto>, ListProductsQueryHandler>();
        services.AddScoped<IValidator<ListProductsQuery>, ListProductsQueryValidator>();
```

`GetOrderQuery`, `GetCustomerQuery` and `GetCategoryQuery` follow `GetProductQuery` with `IOrderReader`, `ICustomerReader` and `ICategoryReader` and their views (`OrderView` holds its `OrderLineView`s). A list query is added only where a screen needs one; the example has `ListProductsQuery`.

## Infrastructure: the readers

File `src/{Name}.Infrastructure/Persistence/Products/ProductReader.cs`, registered by `AddPersistenceModule` (`services.AddScoped<IProductReader, ProductReader>();`):

```csharp
public class ProductReader(AppDbContext context) : IProductReader
{
    private static readonly Expression<Func<Product, ProductView>> AsView = product => new ProductView(product.Id, product.Sku, product.Name, product.Stock, product.CategoryId);

    public Task<ProductView> FindAsync(Guid id)
    {
        return context.Set<Product>().AsNoTracking().Where(product => product.Id == id).Select(AsView).FirstOrDefaultAsync();
    }

    public async Task<PagedResult<ProductView>> ListAsync(ProductListFilter filter)
    {
        var total = await Filtered(filter).CountAsync();
        var items = await Page(filter).ToListAsync();

        return new PagedResult<ProductView>(items, filter.Page, filter.Size, total);
    }

    public IQueryable<ProductView> Page(ProductListFilter filter)
    {
        var filtered = Filtered(filter);

        var sorted = filter.Sort switch
        {
            ProductSort.NameAscending => filtered.OrderBy(product => product.Name).ThenBy(product => product.Id),
            ProductSort.StockDescending => filtered.OrderByDescending(product => product.Stock).ThenBy(product => product.Id),
            _ => filtered.OrderBy(product => product.Sku)
        };

        return sorted.Skip(PageRequest.Skip(filter.Page, filter.Size)).Take(filter.Size).Select(AsView);
    }

    private IQueryable<Product> Filtered(ProductListFilter filter)
    {
        var products = context.Set<Product>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.NameContains))
        {
            products = products.Where(product => EF.Functions.ILike(product.Name, "%" + EscapeLike(filter.NameContains) + "%", "\\"));
        }

        if (filter.CategoryId is not null)
        {
            products = products.Where(product => product.CategoryId == filter.CategoryId);
        }

        return products;
    }

    private static string EscapeLike(string text)
    {
        return text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
```

- The page is one statement that selects only the view's columns; the total is a second `count(*)` with the same filter. `Page` is public so a test can read its SQL.
- Every sort ends on a unique column, so pages do not overlap.
- With `languages.mode` = `data`, `Name` is a `LocalizedText` in jsonb: the view carries the `LocalizedText` and `ProductDto.From` takes the caller's language (`languages.md`), and a name filter or sort reads the language's path. With tenancy the query filter of `AppDbContext` applies to readers too. With MongoDB a reader uses a projection on the collection through the repository base (`infrastructure-mongo.md`).

`OrderReader` projects the lines in the same statement, and computes the total the way `Order.TotalAmount` does (`OrderView` and `OrderLineView` are records with these fields, in the same order):

```csharp
    private static readonly Expression<Func<Order, OrderView>> AsView = order => new OrderView(
        order.Id,
        order.OrderNumber,
        order.CustomerId,
        order.Status.ToString(),
        order.Lines.Where(line => !line.IsCancelled).Sum(line => line.Price.Amount * line.Quantity),
        order.Lines.Select(line => new OrderLineView(line.Id, line.Sku, line.Price.Amount, line.Price.Currency, line.Quantity, line.IsCancelled, line.CancellationReason)).ToList());
```

`Lines` is a read-only collection over a backing field: the reader projects through the navigation EF knows (`order_lines`), never through `Order.TotalAmount`, which EF cannot translate.

## The hosts

Controllers take `IQueryDispatcher` and bind the query from the route or the query string, so the filter fills its context:

```csharp
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] GetProductQuery query)
    {
        return Ok(await queryDispatcher.Ask(query));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListProductsQuery query)
    {
        return Ok(await queryDispatcher.Ask(query));
    }
```

`ContextCommandFilter` fills queries as well as commands: after the command loop, `foreach (var query in context.ActionArguments.Values.OfType<IQuery>()) query.Context = commandContext;`.

The Console runs queries by name like commands. `ConsoleCommandRunner` looks for `{name}Command` first, then `{name}Query`, and asks a query through the dispatcher:

```csharp
        var queryType = applicationAssembly.GetTypes().SingleOrDefault(type => type.Name == name + "Query" && typeof(IQuery).IsAssignableFrom(type));

        if (queryType is not null)
        {
            var query = (IQuery)Deserialize(queryType, argument);

            query.Context = commandContext;

            return await (Task<object>)typeof(ConsoleCommandRunner).GetMethod(nameof(AskAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(ResultTypeOf(queryType)).Invoke(this, [query]);
        }
```

`AskAsync<TResult>(IQuery<TResult> query)` awaits `queryDispatcher.Ask(query)` and returns the result as `object`; `ResultTypeOf` reads `TResult` from the query's `IQuery<>` interface. The runner takes `IQueryDispatcher` next to `ICommandDispatcher`.

## Tests

`test/{Name}.Tests/Application/QueryRulesTests.cs`, the architecture test:

```csharp
public class QueryRulesTests
{
    [Fact]
    public void QueryHandlers_Should_DependOnNoUnitOfWorkRepositoryOrDomainService()
    {
        var forbidden = new Func<Type, bool>(type =>
            type == typeof(IUnitOfWork)
            || type.GetInterfaces().Concat([type]).Any(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRepository<,>))
            || type.Namespace?.StartsWith("{Name}.Domain") == true && type.Name.EndsWith("Service"));

        var offenders = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.BaseType is { IsGenericType: true } baseType && baseType.GetGenericTypeDefinition() == typeof(BaseQueryHandler<,>))
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Where(parameter => forbidden(parameter.ParameterType)).Select(parameter => $"{type.Name}({parameter.ParameterType.Name})"));

        Assert.Empty(offenders);
    }
}
```

`test/{Name}.Tests/Infrastructure/Persistence/Products/ProductReaderTests.cs`, the SQL of a page, read without a database:

```csharp
public class ProductReaderTests
{
    [Fact]
    public void Page_Should_SelectOnlyTheViewColumnsWithTheFilterSortAndPageInOneStatement()
    {
        var productReader = new ProductReader(AppDbContextBuilder.Create().Build());

        var sql = productReader.Page(new ProductListFilter(2, 10, ProductSort.NameAscending, "bolt", null)).ToQueryString();

        Assert.Contains("ILIKE", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("created_user_id", sql);
        Assert.Single(Regex.Matches(sql, @"\bSELECT\b"));
    }
}
```

`GetProductQueryHandlerTests` and `ListProductsQueryHandlerTests` use a `ProductReaderBuilder` (a Moq builder like the repository builders): not found throws `NotFoundException`; a size of 0 or above `PageRequest.MaxSize` throws `ValidationFailedException` before the reader is called. `scripts/conformance/queries.sh` proves the rest over HTTP.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `IQuery.cs`, `IQueryDispatcher.cs`, `PageRequest.cs`, `PagedResult.cs` | `{Name}.Shared.Application` | none |
| `BaseQueryHandler.cs` | same | `FluentValidation`, `{Name}.Shared.Exceptions` |
| `QueryDispatcher.cs`, `QueryHandlerExtensions.cs` | same | `Microsoft.Extensions.DependencyInjection`, `System.Collections.Concurrent` |
| `IProductReader.cs`, `ProductView.cs` | `{Name}.Domain.Aggregates.Products` | `{Name}.Shared.Application` |
| `GetProductQuery.cs`, `ListProductsQuery.cs` | `{Name}.Application.Products` | `System.Text.Json.Serialization`, `FluentValidation`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Application`, `{Name}.Shared.Exceptions` |
| `ProductReader.cs` | `{Name}.Infrastructure.Persistence.Products` | `System.Linq.Expressions`, `Microsoft.EntityFrameworkCore`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Application` |

## Decision record

`docs/adr/0018-queries-and-read-models.md`, in `project-docs-adr.md`.
