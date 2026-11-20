using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Application.Products;
using Moq;

namespace Boilerplate.Tests.Application.Products;

public class DecreaseStockWhenOrderLineAddedEventHandlerTests
{
    [Fact]
    public async Task Handle_Should_DecreaseTheStockOfTheProductWithTheSkuAndQuantityOfTheEvent()
    {
        Product writtenProduct = null;
        var product = FakeProduct.Create("SKU-1", 10);
        var productRepositoryBuilder = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(product)
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
        var productRepository = productRepositoryBuilder.Build();
        var commandPipeline = CommandPipelineBuilder<DecreaseProductStockCommand>.Create().WithValidator(new DecreaseProductStockCommandValidator()).Build();
        var eventHandler = new DecreaseStockWhenOrderLineAddedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(new DecreaseProductStockCommandHandler(productRepository, commandPipeline)).Build());

        await eventHandler.Handle(new LineAdded(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3));

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.FindBySkuTrackingAsync("SKU-1"), Times.Once);
        Assert.Equal(7, writtenProduct.Stock);
    }

    [Fact]
    public async Task Handle_Should_RunTheCommandAsTheSystemUserWhateverUserRaisedTheEvent()
    {
        Product writtenProduct = null;
        var productRepositoryBuilder = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 10))
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
        var productRepository = productRepositoryBuilder.Build();
        var commandPipeline = CommandPipelineBuilder<DecreaseProductStockCommand>.Create().WithValidator(new DecreaseProductStockCommandValidator()).Build();
        var eventHandler = new DecreaseStockWhenOrderLineAddedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(new DecreaseProductStockCommandHandler(productRepository, commandPipeline)).Build());

        await eventHandler.Handle(new LineAdded(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3) { UserId = "user-7", UserEmail = "user-7@localhost" });

        Assert.Equal([SystemUser.Id, SystemUser.Email], [writtenProduct.UpdatedUserId, writtenProduct.UpdatedUserName]);
    }
}
