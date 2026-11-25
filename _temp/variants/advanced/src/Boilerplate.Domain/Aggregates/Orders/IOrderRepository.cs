using Boilerplate.Shared.Persistence;

namespace Boilerplate.Domain.Aggregates.Orders;

public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<IReadOnlyList<Order>> FindOpenWithAllLinesCancelledTrackingAsync(int limit);
}
