using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Customers;

public class CustomerRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Customer, Guid>(context, unitOfWork, eventContext), ICustomerRepository
{
    public Task<bool> IsEmailExistsAsync(string email)
    {
        return Query().AnyAsync(customer => customer.Email == email);
    }
}
