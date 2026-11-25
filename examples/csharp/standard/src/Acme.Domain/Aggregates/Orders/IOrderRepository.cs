using Acme.Shared.Persistence;

namespace Acme.Domain.Aggregates.Orders;

public interface IOrderRepository : IRepository<Order, Guid>;
