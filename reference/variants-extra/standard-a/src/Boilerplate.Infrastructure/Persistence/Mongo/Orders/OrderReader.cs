using Boilerplate.Domain.Aggregates.Orders;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Orders;

public class OrderReader(IMongoDatabase database) : IOrderReader
{
    private readonly IMongoCollection<Order> _orders = database.GetCollection<Order>("orders");

    public async Task<OrderView> FindAsync(Guid id)
    {
        var order = await _orders.Find(candidate => candidate.Id == id).FirstOrDefaultAsync();

        return order is null ? null : new OrderView(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Status.ToString(),
            order.TotalAmount,
            order.Lines.Select(line => new OrderLineView(line.Id, line.Sku, line.Price.Amount, line.Price.Currency, line.Quantity, line.IsCancelled, line.CancellationReason)).ToList());
    }
}
