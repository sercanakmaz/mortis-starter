using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class InvalidateProductCacheWhenRenamedEventHandler(ICacheService cacheService) : BaseSyncEventHandler<Renamed>
{
    protected override async Task OnHandle(Renamed @event)
    {
        await cacheService.RemoveByTagAsync(ProductCacheKeys.Tag(@event.TenantId, @event.Id));
    }
}
