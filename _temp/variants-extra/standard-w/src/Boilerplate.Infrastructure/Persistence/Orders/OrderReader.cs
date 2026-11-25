using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Infrastructure.Persistence.Orders;

public class OrderReader(AppDbContext context) : IOrderReader
{
    private static readonly Expression<Func<Order, OrderView>> AsView = order => new OrderView(
        order.Id,
        order.OrderNumber,
        order.CustomerId,
        order.Status.ToString(),
        order.Lines.Where(line => !line.IsCancelled).Sum(line => line.Price.Amount * line.Quantity),
        order.Lines.Select(line => new OrderLineView(line.Id, line.Sku, line.Price.Amount, line.Price.Currency, line.Quantity, line.IsCancelled, line.CancellationReason)).ToList());

    public Task<OrderView> FindAsync(Guid id)
    {
        return context.Set<Order>().AsNoTracking().Where(order => order.Id == id).Select(AsView).FirstOrDefaultAsync();
    }
}
