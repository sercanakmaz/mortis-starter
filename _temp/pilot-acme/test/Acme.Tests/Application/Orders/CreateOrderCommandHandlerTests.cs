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

public class CreateOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnTheCreatedOrderInsideATransaction()
    {
        var calls = new List<string>();
        var customerId = Guid.CreateVersion7();
        var orderRepository = OrderRepositoryBuilder.Create().WithCreateAsync(_ => calls.Add(nameof(IOrderRepository.CreateAsync))).Build();
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
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IOrderRepository.CreateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithoutLines_Should_ThrowValidationFailedForLinesAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithCreateAsync();
        var commandPipeline = CommandPipelineBuilder<CreateOrderCommand>.Create().WithValidator(new CreateOrderCommandValidator()).Build();
        var commandHandler = new CreateOrderCommandHandler(orderRepositoryBuilder.Build(), commandPipeline);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new CreateOrderCommand
        {
            OrderNumber = "ORD-1",
            CustomerId = Guid.CreateVersion7(),
            Lines = []
        }));

        Assert.Equal([nameof(CreateOrderCommand.Lines)], exception.Errors.Keys);
        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.CreateAsync(It.IsAny<Order>()), Times.Never);
    }
}
