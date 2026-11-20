using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Application.Products;
using Moq;

namespace Boilerplate.Tests.Application.Products;

public class IncreaseStockWhenOrderLineCancelledEventHandlerTests
{
    [Fact]
    public async Task Handle_Should_IncreaseTheStockOfTheProductWithTheSkuAndQuantityOfTheEvent()
    {
        Product writtenProduct = null;
        var product = FakeProduct.Create("SKU-1", 7);
        var productRepositoryBuilder = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(product)
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
        var productRepository = productRepositoryBuilder.Build();
        var commandPipeline = CommandPipelineBuilder<IncreaseProductStockCommand>.Create().WithValidator(new IncreaseProductStockCommandValidator()).Build();
        var eventHandler = new IncreaseStockWhenOrderLineCancelledEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(new IncreaseProductStockCommandHandler(productRepository, commandPipeline)).Build());

        await eventHandler.Handle(new LineCancelled(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3, "changed mind"));

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.FindBySkuTrackingAsync("SKU-1"), Times.Once);
        Assert.Equal(10, writtenProduct.Stock);
    }

    [Fact]
    public async Task Handle_Should_RunTheCommandAsTheSystemUserWhateverUserRaisedTheEvent()
    {
        Product writtenProduct = null;
        var productRepositoryBuilder = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 7))
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
        var productRepository = productRepositoryBuilder.Build();
        var commandPipeline = CommandPipelineBuilder<IncreaseProductStockCommand>.Create().WithValidator(new IncreaseProductStockCommandValidator()).Build();
        var eventHandler = new IncreaseStockWhenOrderLineCancelledEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(new IncreaseProductStockCommandHandler(productRepository, commandPipeline)).Build());

        await eventHandler.Handle(new LineCancelled(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3, "changed mind") { UserId = "user-7", UserEmail = "user-7@localhost" });

        Assert.Equal([SystemUser.Id, SystemUser.Email], [writtenProduct.UpdatedUserId, writtenProduct.UpdatedUserName]);
    }
}
