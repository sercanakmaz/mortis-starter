using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;
using Boilerplate.Infrastructure.Persistence.Products;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.Infrastructure.Persistence.Products;

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

        var stockDecreased = Assert.IsType<ProductStockDecreased>(eventContext.TakeRaised());
        Assert.Equal(8, stockDecreased.Stock);
        Assert.Empty(product.DomainEvents);
    }
}
