# Caching, C#

Applies when `addons` contains `caching`. Projects `{Name}.Shared` (folder `Application/`), `{Name}.Infrastructure` (folder `Caching/`) and, with the example, the product slice. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

Package, in `{Name}.Infrastructure`: `Microsoft.Extensions.Caching.Hybrid`; with Redis also `Microsoft.Extensions.Caching.StackExchangeRedis`.

## How it fits together

```
read handler (Get*Command / a query)   cache.GetOrCreateAsync(key, tags, load)   load = the repository read and the mapping
key        {plural}:{tenant}:{id}:{language}         everything the answer depends on
tag        {aggregate}:{tenant}:{id}                 one tag per aggregate instance, shared by its keys
write      the aggregate raises an event -> a sync handler: cache.RemoveByTagAsync(tag)   (inside the command)
storage    HybridCache: in-memory in each process; Redis behind it when ConnectionStrings:Redis is set
```

- Only read answers are cached, never the aggregate a command loads to change: a command always reads the database, so a stale cache can never feed a write.
- Every change a cached answer shows must raise an event, because the event is what invalidates. In the example `Product.Rename` gains a `Renamed` event for this reason.
- The sync handler runs inside the command's transaction, before the commit. A read that lands between the removal and the commit can put the old answer back; the entry's lifetime (`Caching:Seconds`, 60 by default) bounds that window. Sequential callers never see a stale answer: the command has returned only after the removal.
- With several API processes and no Redis, each process has its own memory cache and a removal reaches only the process that ran the command; the lifetime bounds the staleness of the others. Set Redis when that matters.
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

`GetProductCommandHandler` takes `ICacheService` and reads through it:

```csharp
    protected override async Task<CommandResult<ProductDto>> OnHandle(GetProductCommand command)
    {
        var tenantId = command.Context?.TenantId;
        var language = command.Context?.Language;

        var productDto = await cacheService.GetOrCreateAsync(ProductCacheKeys.ById(tenantId, command.Id, language), [ProductCacheKeys.Tag(tenantId, command.Id.ToString())], async _ =>
        {
            var product = await productRepository.FindByIdAsync(command.Id)
                ?? throw new NotFoundException(nameof(Product));

            return ProductDto.Map(product, language);
        });

        return CommandResult.FromContent(productDto);
    }
```

A thrown `NotFoundException` is not cached: HybridCache stores only returned values.

One sync handler per event that changes what `ProductDto` shows, file `src/{Name}.Application/Products/InvalidateProductCacheWhenStockDecreasedEventHandler.cs`:

```csharp
public class InvalidateProductCacheWhenStockDecreasedEventHandler(ICacheService cacheService) : BaseSyncEventHandler<StockDecreased>
{
    protected override async Task OnHandle(StockDecreased @event)
    {
        await cacheService.RemoveByTagAsync(ProductCacheKeys.Tag(@event.TenantId, @event.Id));
    }
}
```

The same for `StockIncreased` and `Renamed` (and `Deleted`, `Restored` with soft delete), each registered with `services.AddScoped<BaseSyncEventHandler<StockDecreased>, InvalidateProductCacheWhenStockDecreasedEventHandler>();`. These handlers send no command: they are the second kind of handler, after read models, that only maintains something outside the aggregates.

## Tests

`HybridCacheServiceTests` (a real `HybridCache` from a `ServiceCollection`): the loader runs once for two reads of one key; after `RemoveByTagAsync` it runs again; two keys with one tag both reload. `GetProductCommandHandlerTests` gains a test with a pass-through `CacheServiceBuilder` (the loader always runs) so the handler's mapping stays tested. Each invalidation handler has a test that it removes the tag of its event's tenant and id. `scripts/conformance/caching.sh` proves freshness over HTTP.

## Files this add-on changes

`GetProductCommand.cs`, `Product.cs` (the `Renamed` event), `ApplicationModule.cs`, `InfrastructureModule.cs`, `appsettings.json` (`"Caching": { "Seconds": 60 }`), `docker-compose.yml` (a `redis` service, commented out by default).

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `ICacheService.cs` | `{Name}.Shared.Application` | none |
| `HybridCacheService.cs`, `CachingModule.cs`, `CachingSettings.cs` | `{Name}.Infrastructure.Caching` | `{Name}.Shared.Application`, `Microsoft.Extensions.Caching.Hybrid`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options` |
| `ProductCacheKeys.cs`, `Invalidate*EventHandler.cs` | `{Name}.Application.Products` | `{Name}.Domain.Aggregates.Products.Events`, `{Name}.Shared.Application`, `{Name}.Shared.Domain` |

## Decision record

`docs/adr/0025-caching-reads.md`, in `project-docs-adr.md`.
