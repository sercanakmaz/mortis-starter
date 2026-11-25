using Microsoft.EntityFrameworkCore;
using Boilerplate.Domain.Aggregates.Customers;

namespace Boilerplate.Infrastructure.Persistence.Customers;

public class CustomerReader(AppDbContext context) : ICustomerReader
{
    public Task<CustomerView> FindAsync(Guid id)
    {
        return context.Set<Customer>().AsNoTracking()
            .Where(customer => customer.Id == id)
            .Select(customer => new CustomerView(customer.Id, customer.Name, customer.Email, customer.IsBlocked, customer.CompletedOrderCount, customer.Note, customer.TaxNumber))
            .FirstOrDefaultAsync();
    }
}
