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

public class CancelOrderLineCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CancelTheLineAndUpdateTheOrder()
    {
        var calls = new List<string>();
        Order writtenOrder = null;
        var fakeOrder = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithoutEvents();
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(fakeOrder)
            .WithUpdateAsync(updatedOrder =>
            {
                writtenOrder = updatedOrder;
                calls.Add(nameof(IOrderRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<CancelOrderLineCommand>.Create()
            .WithValidator(new CancelOrderLineCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CancelOrderLineCommandHandler(orderRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new CancelOrderLineCommand { Id = fakeOrder.Id, LineId = fakeOrder.Lines.First().Id, Reason = "out of stock", Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.True(commandResult.Content.Lines.First().IsCancelled);
        Assert.Equal("user-1", writtenOrder.UpdatedUserId);
        Assert.IsType<LineCancelled>(Assert.Single(writtenOrder.DomainEvents));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithAnUnknownLine_Should_ThrowNotFoundAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(FakeOrder.Create().WithLine("SKU-1")).WithUpdateAsync();
        var commandHandler = new CancelOrderLineCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<CancelOrderLineCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new CancelOrderLineCommand { Id = Guid.CreateVersion7(), LineId = Guid.CreateVersion7(), Reason = "out of stock" }));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }
}
