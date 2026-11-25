using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class InvalidateProductCacheWhenRestoredEventHandler(ICacheService cacheService) : BaseSyncEventHandler<Restored>
{
    protected override async Task OnHandle(Restored @event)
    {
        await cacheService.RemoveByTagAsync(ProductCacheKeys.Tag(@event.TenantId, @event.Id));
    }
}
