using Boilerplate.Domain.Aggregates.Customers;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Customers;

public class CustomerReader(IMongoDatabase database) : ICustomerReader
{
    private readonly IMongoCollection<Customer> _customers = database.GetCollection<Customer>("customers");

    public async Task<CustomerView> FindAsync(Guid id)
    {
        var customer = await _customers.Find(candidate => candidate.Id == id).FirstOrDefaultAsync();

        return customer is null ? null : new CustomerView(customer.Id, customer.Name, customer.Email, customer.IsBlocked, customer.CompletedOrderCount, customer.Note);
    }
}
