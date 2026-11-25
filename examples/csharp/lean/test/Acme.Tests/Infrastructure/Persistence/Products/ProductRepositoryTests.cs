using Acme.Domain.Aggregates.Products.Events;
using Acme.Infrastructure.Persistence.Products;
using Acme.Shared.Domain;
using Acme.Tests.TestSupport.Domain.Products;
using Acme.Tests.TestSupport.Infrastructure.Persistence;
using Acme.Tests.TestSupport.Shared.Persistence;

namespace Acme.Tests.Infrastructure.Persistence.Products;

public class ProductRepositoryTests
{
    [Fact]
    public async Task UpdateAsync_WithATrackedProductWhoseStockChanged_Should_HandStockDecreasedToTheEventContext()
    {
        var eventContext = new EventContext();
        var context = AppDbContextBuilder.Create().Build();
        var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();
        context.Attach(product);
        product.DecreaseStock(2);
        var unitOfWork = UnitOfWorkBuilder.Create().WithHasTransaction(true).WithSaveChangesAsync().Build();
        var productRepository = new ProductRepository(context, unitOfWork, eventContext);

        await productRepository.UpdateAsync(product);

        var stockDecreased = Assert.IsType<StockDecreased>(eventContext.TakeRaised());
        Assert.Equal(8, stockDecreased.Stock);
        Assert.Empty(product.DomainEvents);
    }
}
