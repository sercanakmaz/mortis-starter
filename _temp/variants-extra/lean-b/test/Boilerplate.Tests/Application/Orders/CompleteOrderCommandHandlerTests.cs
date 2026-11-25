using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Application.Orders;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using Moq;

namespace Boilerplate.Tests.Application.Orders;

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

        var commandResult = await commandHandler.Handle(new CompleteOrderCommand(1) { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal("Completed", commandResult.Content.Status);
        Assert.Equal("user-1", writtenOrder.UpdatedUserId);
        Assert.IsType<OrderCompleted>(Assert.Single(writtenOrder.DomainEvents));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithAnUnknownOrder_Should_ThrowNotFoundAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).WithUpdateAsync();
        var commandHandler = new CompleteOrderCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<CompleteOrderCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new CompleteOrderCommand(1)));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }
}
