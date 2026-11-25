using Acme.Shared.Persistence;

namespace Acme.Domain.Aggregates.Customers;

public interface ICustomerRepository : IRepository<Customer, Guid>;
