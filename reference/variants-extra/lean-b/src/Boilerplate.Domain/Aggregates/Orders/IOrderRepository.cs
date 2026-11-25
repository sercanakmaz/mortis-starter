using Boilerplate.Shared.Persistence;

namespace Boilerplate.Domain.Aggregates.Orders;

public interface IOrderRepository : IRepository<Order, int>;
