using Boilerplate.Contracts.Products;
using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class InvalidateProductCacheWhenStockIncreasedEventHandler(ICacheService cacheService) : BaseSyncEventHandler<StockIncreased>
{
    protected override async Task OnHandle(StockIncreased @event)
    {
        await cacheService.RemoveByTagAsync(ProductCacheKeys.Tag(@event.TenantId, @event.Id));
    }
}
