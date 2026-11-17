using Acme.Application.Orders;
using Acme.Domain.Aggregates.Orders;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Exceptions;
using Acme.Shared.Persistence;
using Acme.Tests.TestSupport.Domain.Orders;
using Acme.Tests.TestSupport.Shared.Application;
using Acme.Tests.TestSupport.Shared.Persistence;
using Moq;

namespace Acme.Tests.Application.Orders;

public class CancelOrderWhenAllLinesCancelledCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithEveryLineCancelled_Should_CancelAndUpdateTheOrder()
    {
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1").WithoutEvents();
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
            .WithUpdateAsync(updatedOrder => writtenOrder = updatedOrder)
            .Build();
        var commandHandler = new CancelOrderWhenAllLinesCancelledCommandHandler(orderRepository, CommandPipelineBuilder<CancelOrderWhenAllLinesCancelledCommand>.Create().Build());

        var commandResult = await commandHandler.Handle(new CancelOrderWhenAllLinesCancelledCommand(order.Id) { UserId = "user-1", UserEmail = "user-1@localhost" });

        Assert.Equal("Cancelled", commandResult.Content.Status);
        Assert.Equal("user-1", writtenOrder.UpdatedUserId);
        Assert.IsType<Cancelled>(Assert.Single(writtenOrder.DomainEvents));
    }

    [Fact]
    public async Task Handle_WithAnActiveLine_Should_ReturnTheOrderWithoutWriting()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithoutEvents();
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(order).WithUpdateAsync();
        var commandHandler = new CancelOrderWhenAllLinesCancelledCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<CancelOrderWhenAllLinesCancelledCommand>.Create().Build());

        var commandResult = await commandHandler.Handle(new CancelOrderWhenAllLinesCancelledCommand(order.Id));

        Assert.Equal("Open", commandResult.Content.Status);
        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }
}
