using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Customers;
using Moq;

namespace Boilerplate.Tests.TestSupport.Domain.Customers;

public class CustomerServiceBuilder
{
    private ICustomerRepository _customerRepository = Mock.Of<ICustomerRepository>();

    public static CustomerServiceBuilder Create() => new();

    public CustomerServiceBuilder WithCustomerRepository(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
        return this;
    }

    public ICustomerService Build()
    {
        return new CustomerService(_customerRepository);
    }
}
