using Boilerplate.Shared.Domain;
using Boilerplate.Domain.Aggregates.Customers;
using Moq;

namespace Boilerplate.Tests.TestSupport.Domain.Customers;

public class CustomerRepositoryBuilder
{
    private readonly Mock<ICustomerRepository> _mock = new();

    public static CustomerRepositoryBuilder Create() => new();

    public CustomerRepositoryBuilder WithFindByIdAsync(Customer customer)
    {
        _mock.Setup(customerRepository => customerRepository.FindByIdAsync(It.IsAny<int>())).ReturnsAsync(customer);
        return this;
    }

    public CustomerRepositoryBuilder WithFindByIdTrackingAsync(Customer customer)
    {
        _mock.Setup(customerRepository => customerRepository.FindByIdTrackingAsync(It.IsAny<int>())).ReturnsAsync(customer);
        return this;
    }


    public CustomerRepositoryBuilder WithCreateAsync(Action<Customer> callback = null)
    {
        _mock.Setup(customerRepository => customerRepository.CreateAsync(It.IsAny<Customer>()))
            .Callback<Customer>(customer =>
            {
                typeof(Entity<int>).GetProperty(nameof(Entity<int>.Id)).SetValue(customer, 1);
                callback?.Invoke(customer);
            })
            .Returns(Task.CompletedTask);
        return this;
    }

    public CustomerRepositoryBuilder WithUpdateAsync(Action<Customer> callback = null)
    {
        _mock.Setup(customerRepository => customerRepository.UpdateAsync(It.IsAny<Customer>()))
            .Callback<Customer>(customer => callback?.Invoke(customer))
            .Returns(Task.CompletedTask);
        return this;
    }

    public ICustomerRepository Build() => _mock.Object;
    public Mock<ICustomerRepository> BuildMock() => _mock;
}
