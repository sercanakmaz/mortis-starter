using Boilerplate.Shared.Persistence;

namespace Boilerplate.Domain.Aggregates.Customers;

public interface ICustomerRepository : IRepository<Customer, int>;
