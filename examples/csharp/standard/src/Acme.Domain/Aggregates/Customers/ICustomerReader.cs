namespace Acme.Domain.Aggregates.Customers;

public interface ICustomerReader
{
    Task<CustomerView> FindAsync(Guid id);
}
