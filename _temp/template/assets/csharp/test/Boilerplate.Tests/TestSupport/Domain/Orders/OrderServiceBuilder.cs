using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Orders;
using Moq;

namespace Boilerplate.Tests.TestSupport.Domain.Orders;

public class OrderServiceBuilder
{
    private IOrderRepository _orderRepository = Mock.Of<IOrderRepository>();

    public static OrderServiceBuilder Create() => new();

    public OrderServiceBuilder WithOrderRepository(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
        return this;
    }

    public IOrderService Build()
    {
        return new OrderService(_orderRepository);
    }
}
