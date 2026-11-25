using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class InvalidateProductCacheWhenDeletedEventHandler(ICacheService cacheService) : BaseSyncEventHandler<Deleted>
{
    protected override async Task OnHandle(Deleted @event)
    {
        await cacheService.RemoveByTagAsync(ProductCacheKeys.Tag(@event.TenantId, @event.Id));
    }
}
