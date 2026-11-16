using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders;

public interface IOrderService
{
    Task<Order> CreateAsync(AuditUser user, Guid id, string orderNumber, Guid customerId, List<OrderLineModel> lines);
    Task<Order> GetAsync(Guid id);
    Task<Order> AddLineAsync(AuditUser user, Guid id, OrderLineModel line);
    Task<Order> CancelLineAsync(AuditUser user, Guid id, Guid lineId, string reason);
    Task<Order> CancelWhenAllLinesCancelledAsync(AuditUser user, Guid id);
    Task<Order> CompleteAsync(AuditUser user, Guid id);
}

public class OrderService(IOrderRepository orderRepository) : IOrderService
{
    public async Task<Order> CreateAsync(AuditUser user, Guid id, string orderNumber, Guid customerId, List<OrderLineModel> lines)
    {
        if (await orderRepository.IsOrderNumberExistsAsync(orderNumber))
        {
            throw new DuplicateException(nameof(Order));
        }

        var order = new Order(id, orderNumber, customerId, user);

        foreach (var line in lines)
        {
            order.AddLine(line.Sku, new Money(line.Price, line.Currency), line.Quantity);
        }

        await orderRepository.CreateAsync(order);

        return order;
    }

    public async Task<Order> GetAsync(Guid id)
    {
        return await orderRepository.FindByIdAsync(id)
            ?? throw new NotFoundException(nameof(Order));
    }

    public async Task<Order> AddLineAsync(AuditUser user, Guid id, OrderLineModel line)
    {
        var order = await orderRepository.FindByIdTrackingAsync(id)
            ?? throw new NotFoundException(nameof(Order));

        order.AddLine(line.Sku, new Money(line.Price, line.Currency), line.Quantity);
        order.SetUpdatedAudit(user);

        await orderRepository.UpdateAsync(order);

        return order;
    }

    public async Task<Order> CancelLineAsync(AuditUser user, Guid id, Guid lineId, string reason)
    {
        var order = await orderRepository.FindByIdTrackingAsync(id)
            ?? throw new NotFoundException(nameof(Order));

        order.CancelLine(lineId, reason);
        order.SetUpdatedAudit(user);

        await orderRepository.UpdateAsync(order);

        return order;
    }

    public async Task<Order> CancelWhenAllLinesCancelledAsync(AuditUser user, Guid id)
    {
        var order = await orderRepository.FindByIdTrackingAsync(id)
            ?? throw new NotFoundException(nameof(Order));

        if (!order.CancelWhenAllLinesCancelled())
        {
            return order;
        }

        order.SetUpdatedAudit(user);

        await orderRepository.UpdateAsync(order);

        return order;
    }

    public async Task<Order> CompleteAsync(AuditUser user, Guid id)
    {
        var order = await orderRepository.FindByIdTrackingAsync(id)
            ?? throw new NotFoundException(nameof(Order));

        order.Complete();
        order.SetUpdatedAudit(user);

        await orderRepository.UpdateAsync(order);

        return order;
    }
}
