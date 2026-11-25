using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;

namespace Acme.Infrastructure.Persistence.Orders;

public class OrderRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Order, Guid>(context, unitOfWork, eventContext), IOrderRepository;
