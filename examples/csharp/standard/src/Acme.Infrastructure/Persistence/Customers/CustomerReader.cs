using System.Linq.Expressions;
using Acme.Domain.Aggregates.Customers;
using Microsoft.EntityFrameworkCore;

namespace Acme.Infrastructure.Persistence.Customers;

public class CustomerReader(AppDbContext context) : ICustomerReader
{
    private static readonly Expression<Func<Customer, CustomerView>> AsView = customer => new CustomerView(customer.Id, customer.Name, customer.Email, customer.IsBlocked, customer.CompletedOrderCount, customer.Note);

    public Task<CustomerView> FindAsync(Guid id)
    {
        return context.Set<Customer>().AsNoTracking().Where(customer => customer.Id == id).Select(AsView).FirstOrDefaultAsync();
    }
}
