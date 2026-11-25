# Caching, C#

Applies when `addons` contains `caching`. Projects `{Name}.Shared` (folder `Application/`), `{Name}.Infrastructure` (folder `Caching/`) and, with the example, the product slice. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Built** in the advanced reference solution (decision 52; the query side, so the cached read is `GetProductQuery`): compiled, tested and verified there.

Packages, in `{Name}.Infrastructure`: `Microsoft.Extensions.Caching.Hybrid` 10.11.1 and `Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.15 (used only when `ConnectionStrings:Redis` is set).

## How it fits together

```
read handler (GetProductQuery, or Get*Command with queries = commands)   cache.GetOrCreateAsync(key, tags, load)   load = the repository read and the mapping
key        {plural}:{tenant}:{id}:{language}         everything the answer depends on
tag        {aggregate}:{tenant}:{id}                 one tag per aggregate instance, shared by its keys
write      the aggregate raises an event -> a sync handler: cache.RemoveByTagAsync(tag)   (inside the command)
storage    HybridCache: in-memory in each process; Redis behind it when ConnectionStrings:Redis is set
```

- Only read answers are cached, never the aggregate a command loads to change: a command always reads the database, so a stale cache can never feed a write.
- Every change a cached answer shows must raise an event, because the event is what invalidates. In the example `Product.Rename` gains a `Renamed` event for this reason.
- The sync handler runs inside the command's transaction, before the commit. A read that lands between the removal and the commit can put the old answer back; the entry's lifetime (`Caching:Seconds`, 60 by default) bounds that window. Sequential callers never see a stale answer: the command has returned only after the removal.
- With several processes and no Redis, each has its own memory cache and a removal reaches only the process that ran the command; the lifetime bounds the staleness of the others. With `hosts.worker` = `separate` the stock reactions run in the Worker, so the API sees a stock change only when its entry expires, unless Redis is set. HybridCache removes tags locally; check its documentation for the version you use before relying on Redis to carry a removal to other processes.
- With tenancy the tenant is part of every key and tag, so one tenant can never read another's entry.

## ICacheService

File `src/{Name}.Shared/Application/ICacheService.cs`:

```csharp
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string key, IReadOnlyCollection<string> tags, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken = default);
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
```

File `src/{Name}.Infrastructure/Caching/HybridCacheService.cs`:

```csharp
public class HybridCacheService(HybridCache hybridCache, IOptions<CachingSettings> settings) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(string key, IReadOnlyCollection<string> tags, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken = default)
    {
        var lifetime = TimeSpan.FromSeconds(settings.Value.Seconds);

        return await hybridCache.GetOrCreateAsync(key, async token => await load(token), new HybridCacheEntryOptions { Expiration = lifetime, LocalCacheExpiration = lifetime }, tags, cancellationToken);
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveByTagAsync(tag, cancellationToken);
    }
}
```

File `src/{Name}.Infrastructure/Caching/CachingModule.cs`:

```csharp
public static class CachingModule
{
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CachingSettings>(configuration.GetSection(CachingSettings.Section));
        services.AddHybridCache();

        if (configuration.GetConnectionString(CachingSettings.RedisConnectionStringName) is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        }

        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }
}
```

`CachingSettings` holds `Section = "Caching"`, `RedisConnectionStringName = "Redis"` and `Seconds` (60). `InfrastructureModule` calls `services.AddCaching(configuration);`. HybridCache uses a registered `IDistributedCache` as its second level automatically.

## The example

File `src/{Name}.Application/Products/ProductCacheKeys.cs`:

```csharp
public static class ProductCacheKeys
{
    public static string ById(string tenantId, Guid id, string language) => $"products:{tenantId}:{id}:{language}";

    public static string Tag(string tenantId, string id) => $"product:{tenantId}:{id}";
}
```

`GetProductQueryHandler` takes `ICacheService` and reads through it:

```csharp
public class GetProductQueryHandler(IProductReader productReader, ICacheService cacheService, IEnumerable<IValidator<GetProductQuery>> validators) : BaseQueryHandler<GetProductQuery, ProductDto>(validators)
{
    protected override async Task<ProductDto> OnHandle(GetProductQuery query)
    {
        var tenantId = query.Context?.TenantId;
        var language = query.Context?.Language;

        return await cacheService.GetOrCreateAsync(ProductCacheKeys.ById(tenantId, query.Id, language), [ProductCacheKeys.Tag(tenantId, query.Id.ToString())], async _ =>
        {
            var productView = await productReader.FindAsync(query.Id) ?? throw new NotFoundException(nameof(Product));

            return ProductDto.From(productView, language);
        });
    }
}
```

With `queries` = `commands`, `GetProductCommandHandler` does the same around its repository read. A thrown `NotFoundException` is not cached: HybridCache stores only returned values.

`Product.Rename` raises a new event, `Events/Renamed.cs` (`public record Renamed(string Id, string Sku) : EventBase;`), after changing the name. One sync handler per event that changes what `ProductDto` shows, file `src/{Name}.Application/Products/InvalidateProductCacheWhenStockDecreasedEventHandler.cs`:

```csharp
public class InvalidateProductCacheWhenStockDecreasedEventHandler(ICacheService cacheService) : BaseSyncEventHandler<StockDecreased>
{
    protected override async Task OnHandle(StockDecreased @event)
    {
        await cacheService.RemoveByTagAsync(ProductCacheKeys.Tag(@event.TenantId, @event.Id));
    }
}
```

The same for `StockIncreased` and `Renamed` (and `Deleted`, `Restored` with soft delete), each registered in `ApplicationModule` with `services.AddScoped<BaseSyncEventHandler<StockDecreased>, InvalidateProductCacheWhenStockDecreasedEventHandler>();` (using `{Name}.Domain.Aggregates.Products.Events`). These handlers send no command: they are the second kind of handler, after read models, that only maintains something outside the aggregates.

## Tests

`test/{Name}.Tests/TestSupport/Shared/Application/PassThroughCacheService.cs`, a cache that always loads and records removals:

```csharp
public class PassThroughCacheService : ICacheService
{
    public List<string> RemovedTags { get; } = [];

    public Task<T> GetOrCreateAsync<T>(string key, IReadOnlyCollection<string> tags, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken = default) => load(cancellationToken);

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        RemovedTags.Add(tag);

        return Task.CompletedTask;
    }
}
```

`GetProductQueryHandlerTests` passes `new PassThroughCacheService()` as the second argument; with a handlers level and above, `ScenarioHost` registers it: `_services.AddSingleton<ICacheService, PassThroughCacheService>();`. `test/{Name}.Tests/Infrastructure/Caching/HybridCacheServiceTests.cs`, against a real `HybridCache`:

```csharp
public class HybridCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_TwiceForOneKey_Should_LoadOnce()
    {
        await using var serviceProvider = new ServiceCollection().AddCaching(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        var loads = 0;

        await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));
        var second = await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));

        Assert.Equal((1, 1), (second, loads));
    }

    [Fact]
    public async Task RemoveByTagAsync_Should_MakeEveryKeyWithTheTagLoadAgain()
    {
        await using var serviceProvider = new ServiceCollection().AddCaching(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        var loads = 0;
        await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));
        await cacheService.GetOrCreateAsync("products:t:1:tr", ["product:t:1"], _ => Task.FromResult(++loads));

        await cacheService.RemoveByTagAsync("product:t:1");

        await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));
        await cacheService.GetOrCreateAsync("products:t:1:tr", ["product:t:1"], _ => Task.FromResult(++loads));
        Assert.Equal(4, loads);
    }
}
```

`test/{Name}.Tests/Application/Products/InvalidateProductCacheEventHandlerTests.cs` (drop the `Deleted` and `Restored` lines without soft delete):

```csharp
public class InvalidateProductCacheEventHandlerTests
{
    [Fact]
    public async Task Handle_EachEventThatChangesAProduct_Should_RemoveTheTagOfItsTenantAndId()
    {
        var cacheService = new PassThroughCacheService();

        await new InvalidateProductCacheWhenStockDecreasedEventHandler(cacheService).Handle(new StockDecreased("p-1", "SKU-1", 1, 9) { TenantId = "t-1" });
        await new InvalidateProductCacheWhenStockIncreasedEventHandler(cacheService).Handle(new StockIncreased("p-2", "SKU-2", 1, 11) { TenantId = "t-1" });
        await new InvalidateProductCacheWhenRenamedEventHandler(cacheService).Handle(new Renamed("p-3", "SKU-3") { TenantId = "t-2" });
        await new InvalidateProductCacheWhenDeletedEventHandler(cacheService).Handle(new Deleted("p-4", "SKU-4") { TenantId = "t-2" });
        await new InvalidateProductCacheWhenRestoredEventHandler(cacheService).Handle(new Restored("p-5", "SKU-5") { TenantId = "t-2" });

        Assert.Equal(["product:t-1:p-1", "product:t-1:p-2", "product:t-2:p-3", "product:t-2:p-4", "product:t-2:p-5"], cacheService.RemovedTags);
    }
}
```

`ProductTests` gains `Rename_Should_RaiseRenamed`. `scripts/conformance/caching.sh` proves the cache and freshness over HTTP.

## Files this add-on changes

`GetProductQuery.cs` (or `GetProductCommand.cs`), `Product.cs` (the `Renamed` event), `ApplicationModule.cs`, `InfrastructureModule.cs` (`services.AddCaching(configuration);`, using `{Name}.Infrastructure.Caching`), the API's and the Worker's `appsettings.json` (`"Caching": { "Seconds": 60 }`).

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `ICacheService.cs` | `{Name}.Shared.Application` | none |
| `HybridCacheService.cs`, `CachingModule.cs`, `CachingSettings.cs` | `{Name}.Infrastructure.Caching` | `{Name}.Shared.Application`, `Microsoft.Extensions.Caching.Hybrid`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options` |
| `PassThroughCacheService.cs` | `{Name}.Tests.TestSupport.Shared.Application` | `{Name}.Shared.Application` |
| `HybridCacheServiceTests.cs` | `{Name}.Tests.Infrastructure.Caching` | `{Name}.Infrastructure.Caching`, `{Name}.Shared.Application`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection` |
| `InvalidateProductCacheEventHandlerTests.cs` | `{Name}.Tests.Application.Products` | `{Name}.Application.Products`, `{Name}.Domain.Aggregates.Products.Events`, `{Name}.Tests.TestSupport.Shared.Application` |
| `ProductCacheKeys.cs`, `Invalidate*EventHandler.cs` | `{Name}.Application.Products` | `{Name}.Domain.Aggregates.Products.Events`, `{Name}.Shared.Application`, `{Name}.Shared.Domain` |

## Decision record

`docs/adr/0025-caching-reads.md`, in `project-docs-adr.md`.
