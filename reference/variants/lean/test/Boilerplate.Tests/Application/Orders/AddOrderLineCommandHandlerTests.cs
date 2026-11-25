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

public class AddOrderLineCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_AddTheLineAndUpdateTheOrder()
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
        var commandPipeline = CommandPipelineBuilder<AddOrderLineCommand>.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new AddOrderLineCommandHandler(orderRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new AddOrderLineCommand { Id = Guid.CreateVersion7(), Line = new OrderLineModel("SKU-2", 4m, "EUR", 3), Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal(["SKU-1", "SKU-2"], commandResult.Content.Lines.Select(line => line.Sku));
        Assert.Equal("user-1", writtenOrder.UpdatedUserId);
        Assert.IsType<LineAdded>(Assert.Single(writtenOrder.DomainEvents));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WhenTheOrderIsCompleted_Should_ThrowNotOpenAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(FakeOrder.Create().WithLine("SKU-1").WithCompleted()).WithUpdateAsync();
        var commandHandler = new AddOrderLineCommandHandler(orderRepositoryBuilder.Build(), CommandPipelineBuilder<AddOrderLineCommand>.Create().Build());

        await Assert.ThrowsAsync<BusinessRuleException>(() => commandHandler.Handle(new AddOrderLineCommand { Id = Guid.CreateVersion7(), Line = new OrderLineModel("SKU-2", 4m, "EUR", 3) }));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }
}
