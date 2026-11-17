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

public class CompleteOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CompleteTheTrackedOrderAndUpdateItInsideATransaction()
    {
        var calls = new List<string>();
        Order writtenOrder = null;
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(FakeOrder.Create().WithLine("SKU-1").WithoutEvents())
            .WithUpdateAsync(updatedOrder =>
            {
                writtenOrder = updatedOrder;
                calls.Add(nameof(IOrderRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<CompleteOrderCommand>.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CompleteOrderCommandHandler(orderRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new CompleteOrderCommand(Guid.CreateVersion7()) { UserId = "user-1", UserEmail = "user-1@localhost" });

        Assert.Equal("Completed", commandResult.Content.Status);
        Assert.Equal("user-1", writtenOrder.UpdatedUserId);
        Assert.IsType<Completed>(Assert.Single(writtenOrder.DomainEvents));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithAnUnknownOrder_Should_ThrowNotFoundAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).WithUpdateAsync();
        var commandHandler = new CompleteOrderCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<CompleteOrderCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new CompleteOrderCommand(Guid.CreateVersion7())));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }
}
