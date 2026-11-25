using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Application.Products;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;
using Moq;

namespace Boilerplate.Tests.Application.Products;

public class DecreaseProductStockCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnTheProductWithTheNewStockInsideATransaction()
    {
        var calls = new List<string>();
        var product = FakeProduct.Create("SKU-1", 10);
        var productRepository = ProductRepositoryBuilder.Create().WithFindBySkuTrackingAsync(product).WithUpdateAsync(_ => calls.Add(nameof(IProductRepository.UpdateAsync))).Build();
        var commandPipeline = CommandPipelineBuilder<DecreaseProductStockCommand>.Create()
            .WithValidator(new DecreaseProductStockCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new DecreaseProductStockCommandHandler(productRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new DecreaseProductStockCommand { Sku = "SKU-1", Quantity = 4 });

        Assert.Equal(6, commandResult.Content.Stock);
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IProductRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithZeroQuantity_Should_ThrowValidationFailedForQuantityAndNotWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 10)).WithUpdateAsync();
        var productRepository = productRepositoryBuilder.Build();
        var commandPipeline = CommandPipelineBuilder<DecreaseProductStockCommand>.Create().WithValidator(new DecreaseProductStockCommandValidator()).Build();
        var commandHandler = new DecreaseProductStockCommandHandler(productRepository, commandPipeline);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new DecreaseProductStockCommand { Sku = "SKU-1", Quantity = 0 }));

        Assert.Equal([nameof(DecreaseProductStockCommand.Quantity)], exception.Errors.Keys);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }
}
