using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Orders;

public class OrderRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Order, Guid>(context, unitOfWork, eventContext), IOrderRepository
{
    public Task<bool> IsOrderNumberExistsAsync(string orderNumber)
    {
        return Query().AnyAsync(order => order.OrderNumber == orderNumber);
    }
}
