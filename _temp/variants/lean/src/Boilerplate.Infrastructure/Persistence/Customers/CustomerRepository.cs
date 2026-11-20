using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Customers;

public class CustomerRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Customer, Guid>(context, unitOfWork, eventContext), ICustomerRepository;
