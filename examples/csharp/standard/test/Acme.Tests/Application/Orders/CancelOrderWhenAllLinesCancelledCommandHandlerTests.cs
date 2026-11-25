using Acme.Application.Orders;
using Acme.Domain.Aggregates.Orders;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;
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
        var calls = new List<string>();
        Order writtenOrder = null;
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1").WithoutEvents())
            .WithUpdateAsync(updatedOrder =>
            {
                writtenOrder = updatedOrder;
                calls.Add(nameof(IOrderRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<CancelOrderWhenAllLinesCancelledCommand>.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CancelOrderWhenAllLinesCancelledCommandHandler(orderRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new CancelOrderWhenAllLinesCancelledCommand(Guid.CreateVersion7()) { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal("Cancelled", commandResult.Content.Status);
        Assert.Equal("user-1", writtenOrder.UpdatedUserId);
        Assert.IsType<Cancelled>(Assert.Single(writtenOrder.DomainEvents));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithAnActiveLine_Should_ReturnTheOrderWithoutWriting()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(FakeOrder.Create().WithLine("SKU-1").WithoutEvents()).WithUpdateAsync();
        var commandHandler = new CancelOrderWhenAllLinesCancelledCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<CancelOrderWhenAllLinesCancelledCommand>.Create().Build());

        var commandResult = await commandHandler.Handle(new CancelOrderWhenAllLinesCancelledCommand(Guid.CreateVersion7()));

        Assert.Equal("Open", commandResult.Content.Status);
        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }
}
