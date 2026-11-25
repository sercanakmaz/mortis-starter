using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Orders;

public class OrderRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Order, int>(context, unitOfWork, eventContext), IOrderRepository;
