using Acme.Application.Products;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Exceptions;
using Acme.Shared.Persistence;
using Acme.Tests.TestSupport.Domain.Products;
using Acme.Tests.TestSupport.Shared.Application;
using Acme.Tests.TestSupport.Shared.Persistence;
using Moq;

namespace Acme.Tests.Application.Products;

public class IncreaseProductStockCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnTheProductWithTheNewStockInsideATransaction()
    {
        var calls = new List<string>();
        Product writtenProduct = null;
        var productRepository = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 10).WithoutEvents())
            .WithUpdateAsync(updatedProduct =>
            {
                writtenProduct = updatedProduct;
                calls.Add(nameof(IProductRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<IncreaseProductStockCommand>.Create()
            .WithValidator(new IncreaseProductStockCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new IncreaseProductStockCommandHandler(productRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new IncreaseProductStockCommand { Sku = "SKU-1", Quantity = 3, UserId = "user-1", UserEmail = "user-1@localhost" });

        Assert.Equal(13, commandResult.Content.Stock);
        Assert.Equal("user-1", writtenProduct.UpdatedUserId);
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IProductRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithZeroQuantity_Should_ThrowValidationFailedForQuantityAndNotWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 10)).WithUpdateAsync();
        var commandPipeline = CommandPipelineBuilder<IncreaseProductStockCommand>.Create().WithValidator(new IncreaseProductStockCommandValidator()).Build();
        var commandHandler = new IncreaseProductStockCommandHandler(productRepositoryBuilder.Build(), commandPipeline);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new IncreaseProductStockCommand { Sku = "SKU-1", Quantity = 0 }));

        Assert.Equal([nameof(IncreaseProductStockCommand.Quantity)], exception.Errors.Keys);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }
}
