using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Application.Orders;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;
using Moq;

namespace Boilerplate.Tests.Application.Orders;

public class CreateOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnTheCreatedOrderInsideATransaction()
    {
        var calls = new List<string>();
        var customerId = 7;
        Order writtenOrder = null;
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithCreateAsync(_ => calls.Add(nameof(IOrderRepository.CreateAsync)))
            .WithUpdateAsync(updatedOrder =>
            {
                writtenOrder = updatedOrder;
                calls.Add(nameof(IOrderRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<CreateOrderCommand>.Create()
            .WithValidator(new CreateOrderCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CreateOrderCommandHandler(orderRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new CreateOrderCommand
        {
            OrderNumber = "ORD-1",
            CustomerId = customerId,
            Lines = [new OrderLineModel("SKU-1", 10.5m, "EUR", 2)]
        });

        Assert.Equal("ORD-1", commandResult.Content.OrderNumber);
        Assert.Equal(customerId, commandResult.Content.CustomerId);
        Assert.Equal("Open", commandResult.Content.Status);
        Assert.Equal(21m, commandResult.Content.TotalAmount);
        Assert.Equal("SKU-1", Assert.Single(commandResult.Content.Lines).Sku);
        Assert.Equal(1, commandResult.Content.Id);
        Assert.Equal(["1", "1"], writtenOrder.DomainEvents.Select(domainEvent => domainEvent switch { OrderCreated created => created.Id, OrderLineAdded lineAdded => lineAdded.Id, _ => null }));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.CreateAsync), nameof(IOrderRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithoutLines_Should_ThrowValidationFailedForLinesAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithCreateAsync();
        var orderRepository = orderRepositoryBuilder.Build();
        var commandPipeline = CommandPipelineBuilder<CreateOrderCommand>.Create().WithValidator(new CreateOrderCommandValidator()).Build();
        var commandHandler = new CreateOrderCommandHandler(orderRepository, commandPipeline);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new CreateOrderCommand
        {
            OrderNumber = "ORD-1",
            CustomerId = 7,
            Lines = []
        }));

        Assert.Equal([nameof(CreateOrderCommand.Lines)], exception.Errors.Keys);
        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.CreateAsync(It.IsAny<Order>()), Times.Never);
    }
}
