using Boilerplate.Contracts.Products;
using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Tests.TestSupport.Shared.Application;

namespace Boilerplate.Tests.Application.Products;

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
