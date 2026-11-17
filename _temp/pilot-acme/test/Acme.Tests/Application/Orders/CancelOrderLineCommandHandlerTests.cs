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

public class CancelOrderLineCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CancelTheLineAndUpdateTheOrder()
    {
        var calls = new List<string>();
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithoutEvents();
        var orderLine = order.Lines.First(line => line.Sku == "SKU-1");
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
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

        var commandResult = await commandHandler.Handle(new CancelOrderLineCommand
        {
            Id = order.Id,
            LineId = orderLine.Id,
            Reason = "out of stock",
            UserId = "user-1",
            UserEmail = "user-1@localhost"
        });

        Assert.True(commandResult.Content.Lines.First(line => line.Sku == "SKU-1").IsCancelled);
        Assert.Equal("user-1", writtenOrder.UpdatedUserId);
        Assert.IsType<LineCancelled>(Assert.Single(writtenOrder.DomainEvents));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithAnUnknownLine_Should_ThrowNotFoundAndNotWrite()
    {
        var order = FakeOrder.Create().WithLine("SKU-1");
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(order).WithUpdateAsync();
        var commandHandler = new CancelOrderLineCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<CancelOrderLineCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new CancelOrderLineCommand { Id = order.Id, LineId = Guid.CreateVersion7(), Reason = "out of stock" }));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }
}
