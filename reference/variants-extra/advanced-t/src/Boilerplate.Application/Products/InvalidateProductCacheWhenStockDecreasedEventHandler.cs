using Boilerplate.Contracts.Products;
using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class InvalidateProductCacheWhenStockDecreasedEventHandler(ICacheService cacheService) : BaseSyncEventHandler<StockDecreased>
{
    protected override async Task OnHandle(StockDecreased @event)
    {
        await cacheService.RemoveByTagAsync(ProductCacheKeys.Tag(@event.TenantId, @event.Id));
    }
}
