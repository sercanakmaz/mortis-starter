using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using Moq;

namespace Boilerplate.Tests.Domain.Orders;

public class OrderServiceTests
{
    [Fact]
    public async Task Create_Should_RaiseCreatedAndLineAddedBeforeTheRepositoryWrite()
    {
        var eventsAtWrite = new List<EventBase>();
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithIsOrderNumberExistsAsync(false)
            .WithCreateAsync(order => eventsAtWrite.AddRange(order.DomainEvents))
            .Build();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepository).Build();

        await orderService.CreateAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), "ORD-1", Guid.CreateVersion7(), [new OrderLineModel("SKU-1", 10m, "EUR", 1)]);

        Assert.Equal([typeof(Created), typeof(LineAdded)], eventsAtWrite.Select(@event => @event.GetType()));
    }

    [Fact]
    public async Task Create_Should_AddEveryLineWithItsPrice()
    {
        var id = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var orderRepository = OrderRepositoryBuilder.Create().WithIsOrderNumberExistsAsync(false).WithCreateAsync().Build();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepository).Build();

        var order = await orderService.CreateAsync(FakeAuditUser.Create(), id, "ORD-1", customerId, [new OrderLineModel("SKU-1", 10.5m, "eur", 2), new OrderLineModel("SKU-2", 4m, "EUR", 1)]);

        Assert.Equal(id, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(["SKU-1", "SKU-2"], order.Lines.Select(orderLine => orderLine.Sku));
        Assert.Equal(new Money(10.5m, "EUR"), order.Lines.First().Price);
        Assert.Equal(25m, order.TotalAmount);
        Assert.Equal("test-user-id", order.CreatedUserId);
    }

    [Fact]
    public async Task Create_WithExistingOrderNumber_Should_ThrowDuplicateAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithIsOrderNumberExistsAsync(true).WithCreateAsync();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepositoryBuilder.Build()).Build();

        var exception = await Assert.ThrowsAsync<DuplicateException>(() => orderService.CreateAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), "ORD-1", Guid.CreateVersion7(), [new OrderLineModel("SKU-1", 10m, "EUR", 1)]));

        Assert.Equal("Order already exists.", exception.Message);
        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.CreateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task Get_WithNotExistsOrder_Should_ThrowNotFoundForOrder()
    {
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(OrderRepositoryBuilder.Create().Build()).Build();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => orderService.GetAsync(Guid.CreateVersion7()));

        Assert.Equal("Order was not found.", exception.Message);
    }

    [Fact]
    public async Task Complete_Should_WriteTheCompletedOrderWithTheUpdatedAudit()
    {
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1").WithoutEvents();
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
            .WithUpdateAsync(updatedOrder => writtenOrder = updatedOrder)
            .Build();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepository).Build();

        await orderService.CompleteAsync(FakeAuditUser.Create(), order.Id);

        Assert.Same(order, writtenOrder);
        Assert.Equal(OrderStatus.Completed, writtenOrder.Status);
        Assert.Equal("test-user-id", writtenOrder.UpdatedUserId);
        Assert.IsType<Completed>(Assert.Single(writtenOrder.DomainEvents));
    }

    [Fact]
    public async Task Complete_WithNotExistsOrder_Should_ThrowNotFoundAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithUpdateAsync();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepositoryBuilder.Build()).Build();

        await Assert.ThrowsAsync<NotFoundException>(() => orderService.CompleteAsync(FakeAuditUser.Create(), Guid.CreateVersion7()));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task AddLine_Should_WriteTheOrderWithTheNewLine()
    {
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1", price: 10m).WithoutEvents();
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
            .WithUpdateAsync(updatedOrder => writtenOrder = updatedOrder)
            .Build();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepository).Build();

        await orderService.AddLineAsync(FakeAuditUser.Create(), order.Id, new OrderLineModel("SKU-2", 2.5m, "EUR", 2));

        Assert.Equal(["SKU-1", "SKU-2"], writtenOrder.Lines.Select(orderLine => orderLine.Sku));
        Assert.Equal(15m, writtenOrder.TotalAmount);
    }

    [Fact]
    public async Task AddLine_WithNotExistsOrder_Should_ThrowNotFoundAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithUpdateAsync();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepositoryBuilder.Build()).Build();

        await Assert.ThrowsAsync<NotFoundException>(() => orderService.AddLineAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), new OrderLineModel("SKU-2", 2.5m, "EUR", 2)));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CancelWhenAllLinesCancelled_WithEveryLineCancelled_Should_WriteTheCancelledOrderWithTheUpdatedAudit()
    {
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1").WithoutEvents();
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
            .WithUpdateAsync(updatedOrder => writtenOrder = updatedOrder)
            .Build();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepository).Build();

        await orderService.CancelWhenAllLinesCancelledAsync(FakeAuditUser.Create(), order.Id);

        Assert.Same(order, writtenOrder);
        Assert.Equal(OrderStatus.Cancelled, writtenOrder.Status);
        Assert.Equal("test-user-id", writtenOrder.UpdatedUserId);
        Assert.IsType<Cancelled>(Assert.Single(writtenOrder.DomainEvents));
    }

    [Fact]
    public async Task CancelWhenAllLinesCancelled_WithAnActiveLine_Should_NotWriteTheOrder()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithoutEvents();
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(order).WithUpdateAsync();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepositoryBuilder.Build()).Build();

        await orderService.CancelWhenAllLinesCancelledAsync(FakeAuditUser.Create(), order.Id);

        Assert.Equal(OrderStatus.Open, order.Status);
        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CancelWhenAllLinesCancelled_WithNotExistsOrder_Should_ThrowNotFoundAndNotWrite()
    {
        var orderRepositoryBuilder = OrderRepositoryBuilder.Create().WithUpdateAsync();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepositoryBuilder.Build()).Build();

        await Assert.ThrowsAsync<NotFoundException>(() => orderService.CancelWhenAllLinesCancelledAsync(FakeAuditUser.Create(), Guid.CreateVersion7()));

        orderRepositoryBuilder.BuildMock().Verify(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task CancelLine_Should_WriteTheOrderWithTheCancelledLine()
    {
        Order writtenOrder = null;
        var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithoutEvents();
        var lineId = order.Lines.First().Id;
        var orderRepository = OrderRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(order)
            .WithUpdateAsync(updatedOrder => writtenOrder = updatedOrder)
            .Build();
        var orderService = OrderServiceBuilder.Create().WithOrderRepository(orderRepository).Build();

        await orderService.CancelLineAsync(FakeAuditUser.Create(), order.Id, lineId, "out of stock");

        Assert.True(writtenOrder.Lines.First(orderLine => orderLine.Id == lineId).IsCancelled);
        Assert.False(writtenOrder.Lines.First(orderLine => orderLine.Id != lineId).IsCancelled);
        Assert.Equal(OrderStatus.Open, writtenOrder.Status);
    }
}
