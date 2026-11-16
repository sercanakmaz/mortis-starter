using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Customers.Events;
using Boilerplate.Tests.TestSupport.Domain.Customers;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using Moq;

namespace Boilerplate.Tests.Domain.Customers;

public class CustomerServiceTests
{
    [Fact]
    public async Task Create_Should_RaiseCreatedBeforeTheRepositoryWrite()
    {
        var eventsAtWrite = new List<EventBase>();
        var customerRepository = CustomerRepositoryBuilder.Create()
            .WithIsEmailExistsAsync(false)
            .WithCreateAsync(customer => eventsAtWrite.AddRange(customer.DomainEvents))
            .Build();
        var customerService = CustomerServiceBuilder.Create().WithCustomerRepository(customerRepository).Build();

        await customerService.CreateAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), "Ada", "ada@example.com");

        Assert.IsType<Created>(Assert.Single(eventsAtWrite));
    }

    [Fact]
    public async Task Create_WithExistingEmail_Should_ThrowDuplicateAndNotWrite()
    {
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create().WithIsEmailExistsAsync(true).WithCreateAsync();
        var customerService = CustomerServiceBuilder.Create().WithCustomerRepository(customerRepositoryBuilder.Build()).Build();

        var exception = await Assert.ThrowsAsync<DuplicateException>(() => customerService.CreateAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), "Ada", "ada@example.com"));

        Assert.Equal("Customer already exists.", exception.Message);
        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.CreateAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task Create_Should_CheckTheNormalizedEmail()
    {
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create().WithIsEmailExistsAsync(false).WithCreateAsync();
        var customerService = CustomerServiceBuilder.Create().WithCustomerRepository(customerRepositoryBuilder.Build()).Build();

        await customerService.CreateAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), "Ada", " Ada@Example.com ");

        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.IsEmailExistsAsync("ada@example.com"), Times.Once);
    }

    [Fact]
    public async Task RegisterCompletedOrder_Should_WriteTheCustomerWithTheIncrementedCount()
    {
        Customer writtenCustomer = null;
        var customer = FakeCustomer.Create().WithCompletedOrderCount(1);
        var customerRepository = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(customer)
            .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer)
            .Build();
        var customerService = CustomerServiceBuilder.Create().WithCustomerRepository(customerRepository).Build();

        await customerService.RegisterCompletedOrderAsync(FakeAuditUser.Create(), customer.Id);

        Assert.Same(customer, writtenCustomer);
        Assert.Equal(2, writtenCustomer.CompletedOrderCount);
    }

    [Fact]
    public async Task RegisterCompletedOrder_WhenBlocked_Should_ThrowBlockedAndNotWrite()
    {
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(FakeCustomer.Create().WithIsBlocked(true))
            .WithUpdateAsync();
        var customerService = CustomerServiceBuilder.Create().WithCustomerRepository(customerRepositoryBuilder.Build()).Build();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => customerService.RegisterCompletedOrderAsync(FakeAuditUser.Create(), Guid.CreateVersion7()));

        Assert.Equal(CustomerConstants.BlockedCode, exception.Code);
        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.UpdateAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCompletedOrder_WithNotExistsCustomer_Should_ThrowNotFoundForCustomer()
    {
        var customerService = CustomerServiceBuilder.Create().WithCustomerRepository(CustomerRepositoryBuilder.Create().Build()).Build();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => customerService.RegisterCompletedOrderAsync(FakeAuditUser.Create(), Guid.CreateVersion7()));

        Assert.Equal("Customer was not found.", exception.Message);
    }
}
