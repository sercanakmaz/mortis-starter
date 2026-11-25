namespace Boilerplate.Domain.Aggregates.Orders;

public interface IOrderReader
{
    Task<OrderView> FindAsync(Guid id);
}
