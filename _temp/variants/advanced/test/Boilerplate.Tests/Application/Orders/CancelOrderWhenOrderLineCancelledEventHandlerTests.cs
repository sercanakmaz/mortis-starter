using Boilerplate.Contracts.Orders;
using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Application.Orders;
using Moq;

namespace Boilerplate.Tests.Application.Orders;

public class CancelOrderWhenOrderLineCancelledEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenEveryLineIsCancelled_Should_CancelTheOrderOfTheEvent()
    {
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithCancelledLine("SKU-2").WithoutEvents();
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
            .WithUpdateAsync(updatedOrder => writtenOrder = updatedOrder);
        var orderRepository = orderRepositoryBuilder.Build();
        var commandHandler = new CancelOrderWhenAllLinesCancelledCommandHandler(orderRepository, CommandPipelineBuilder<CancelOrderWhenAllLinesCancelledCommand>.Create().Build());
        var eventHandler = new CancelOrderWhenOrderLineCancelledEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new LineCancelled(order.Id.ToString(), Guid.CreateVersion7().ToString(), "SKU-2", 1, "out of stock"));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.FindByIdTrackingAsync(order.Id), Times.Once);
        Assert.Equal(OrderStatus.Cancelled, writtenOrder.Status);
    }

    [Fact]
    public async Task Handle_WhenAnotherLineIsStillActive_Should_LeaveTheOrderOpenAndNotWrite()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithoutEvents();
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(order).WithUpdateAsync();
        var orderRepository = orderRepositoryBuilder.Build();
        var commandHandler = new CancelOrderWhenAllLinesCancelledCommandHandler(orderRepository, CommandPipelineBuilder<CancelOrderWhenAllLinesCancelledCommand>.Create().Build());
        var eventHandler = new CancelOrderWhenOrderLineCancelledEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new LineCancelled(order.Id.ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 1, "out of stock"));

        Assert.Equal(OrderStatus.Open, order.Status);
        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_RunTheCommandAsTheUserWhoRaisedTheEvent()
    {
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1").WithoutEvents();
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
            .WithUpdateAsync(updatedOrder => writtenOrder = updatedOrder);
        var orderRepository = orderRepositoryBuilder.Build();
        var commandHandler = new CancelOrderWhenAllLinesCancelledCommandHandler(orderRepository, CommandPipelineBuilder<CancelOrderWhenAllLinesCancelledCommand>.Create().Build());
        var eventHandler = new CancelOrderWhenOrderLineCancelledEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new LineCancelled(order.Id.ToString(), Guid.CreateVersion7().ToString(), "SKU-1", 1, "out of stock") { UserId = "user-7", UserEmail = "user-7@localhost" });

        Assert.Equal(["user-7", "user-7@localhost"], [writtenOrder.UpdatedUserId, writtenOrder.UpdatedUserName]);
    }
}
