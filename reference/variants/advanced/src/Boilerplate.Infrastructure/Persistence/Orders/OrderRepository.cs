using Microsoft.EntityFrameworkCore;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Orders;

public class OrderRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Order, Guid>(context, unitOfWork, eventContext), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> FindOpenWithAllLinesCancelledTrackingAsync(int limit)
    {
        return await DbSet
            .Where(order => order.Status == OrderStatus.Open && order.Lines.Any() && order.Lines.All(line => line.IsCancelled))
            .OrderBy(order => order.Id)
            .Take(limit)
            .ToListAsync();
    }
}
