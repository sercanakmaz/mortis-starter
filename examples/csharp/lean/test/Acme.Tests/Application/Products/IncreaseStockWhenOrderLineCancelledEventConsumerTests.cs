using Moq;
using Acme.Application.Products;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Domain;
using Acme.Tests.TestSupport.Domain.Products;
using Acme.Tests.TestSupport.Shared.Application;

namespace Acme.Tests.Application.Products;

public class IncreaseStockWhenOrderLineCancelledEventConsumerTests
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
        var eventConsumer = new IncreaseStockWhenOrderLineCancelledEventConsumer(CommandDispatcherBuilder.Create().WithCommandHandler(new IncreaseProductStockCommandHandler(productRepository, commandPipeline)).Build());

        await eventConsumer.Handle(new LineCancelled(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3, "changed mind"));

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.FindBySkuTrackingAsync("SKU-1"), Times.Once);
        Assert.Equal(10, writtenProduct.Stock);
    }

    [Fact]
    public async Task Handle_Should_RunTheCommandAsTheUserWhoseCommandRaisedTheEvent()
    {
        Product writtenProduct = null;
        var productRepositoryBuilder = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(FakeProduct.Create("SKU-1", 7))
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
        var productRepository = productRepositoryBuilder.Build();
        var commandPipeline = CommandPipelineBuilder<IncreaseProductStockCommand>.Create().WithValidator(new IncreaseProductStockCommandValidator()).Build();
        var eventConsumer = new IncreaseStockWhenOrderLineCancelledEventConsumer(CommandDispatcherBuilder.Create().WithCommandHandler(new IncreaseProductStockCommandHandler(productRepository, commandPipeline)).Build());

        await eventConsumer.Handle(new LineCancelled(Guid.CreateVersion7().ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 3, "changed mind") { UserId = "user-7", UserEmail = "user-7@localhost" });

        Assert.Equal(["user-7", "user-7@localhost"], [writtenProduct.UpdatedUserId, writtenProduct.UpdatedUserName]);
    }
}
