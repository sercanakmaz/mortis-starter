using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;

namespace Acme.Infrastructure.Persistence.Customers;

public class CustomerRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Customer, Guid>(context, unitOfWork, eventContext), ICustomerRepository;
