using Acme.Domain.Aggregates.Orders;
using Moq;

namespace Acme.Tests.TestSupport.Domain.Orders;

public class OrderRepositoryBuilder
{
    private readonly Mock<IOrderRepository> _mock = new();

    public static OrderRepositoryBuilder Create() => new();

    public OrderRepositoryBuilder WithFindByIdAsync(Order order)
    {
        _mock.Setup(orderRepository => orderRepository.FindByIdAsync(It.IsAny<Guid>())).ReturnsAsync(order);
        return this;
    }

    public OrderRepositoryBuilder WithFindByIdTrackingAsync(Order order)
    {
        _mock.Setup(orderRepository => orderRepository.FindByIdTrackingAsync(It.IsAny<Guid>())).ReturnsAsync(order);
        return this;
    }

    public OrderRepositoryBuilder WithCreateAsync(Action<Order> callback = null)
    {
        _mock.Setup(orderRepository => orderRepository.CreateAsync(It.IsAny<Order>()))
            .Callback<Order>(order => callback?.Invoke(order))
            .Returns(Task.CompletedTask);
        return this;
    }

    public OrderRepositoryBuilder WithUpdateAsync(Action<Order> callback = null)
    {
        _mock.Setup(orderRepository => orderRepository.UpdateAsync(It.IsAny<Order>()))
            .Callback<Order>(order => callback?.Invoke(order))
            .Returns(Task.CompletedTask);
        return this;
    }

    public IOrderRepository Build() => _mock.Object;

    public Mock<IOrderRepository> BuildMock() => _mock;
}
