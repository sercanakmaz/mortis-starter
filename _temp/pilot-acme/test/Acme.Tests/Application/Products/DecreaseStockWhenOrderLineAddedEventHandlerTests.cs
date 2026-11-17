using Acme.Application.Products;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Domain;
using Acme.Tests.TestSupport.Domain.Products;
using Acme.Tests.TestSupport.Shared.Application;
using Moq;

namespace Acme.Tests.Application.Products;

public class DecreaseStockWhenOrderLineAddedEventHandlerTests
{
    [Fact]
    public async Task Handle_Should_DecreaseTheStockOfTheProductWithTheSkuAndQuantityOfTheEvent()
    {
        Product writtenProduct = null;
        var productRepositoryBuilder = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 10))
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
        var commandPipeline = CommandPipelineBuilder<DecreaseProductStockCommand>.Create().WithValidator(new DecreaseProductStockCommandValidator()).Build();
        var commandHandler = new DecreaseProductStockCommandHandler(productRepositoryBuilder.Build(), commandPipeline);
        var eventHandler = new DecreaseStockWhenOrderLineAddedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

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
        var commandPipeline = CommandPipelineBuilder<DecreaseProductStockCommand>.Create().WithValidator(new DecreaseProductStockCommandValidator()).Build();
        var commandHandler = new DecreaseProductStockCommandHandler(productRepositoryBuilder.Build(), commandPipeline);
        var eventHandler = new DecreaseStockWhenOrderLineAddedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new LineAdded(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3) { UserId = "user-7", UserEmail = "user-7@localhost" });

        Assert.Equal(SystemUser.Id, writtenProduct.UpdatedUserId);
    }
}
