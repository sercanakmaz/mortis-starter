using Boilerplate.Domain.Aggregates.Customers.Events;
using Boilerplate.Tests.TestSupport.Domain.Customers;
using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Domain.Customers;

public class CustomerTests
{
    [Fact]
    public void Empty_Constructor_Should_Create()
    {
        Assert.NotNull(FakeCustomer.CreateEmpty());
    }

    [Fact]
    public void Constructor_Should_NormalizeTheEmailAndRaiseNothing()
    {
        var customer = new Customer("Ada", "  Ada@Example.COM ", FakeAuditUser.Create());

        Assert.Equal("ada@example.com", customer.Email);
        Assert.Equal(0, customer.Id);
        Assert.Empty(customer.DomainEvents ?? []);
    }

    [Fact]
    public void MarkCreated_WithTheDatabaseId_Should_RaiseCreatedCarryingIt()
    {
        var customer = FakeCustomer.Create(42);

        var created = Assert.IsType<CustomerCreated>(Assert.Single(customer.DomainEvents));
        Assert.Equal("42", created.Id);
        Assert.Equal("default-customer@example.com", created.Email);
    }

    [Fact]
    public void MarkCreated_BeforeTheDatabaseAssignedAnId_Should_Throw()
    {
        var customer = new Customer("Ada", "ada@example.com", FakeAuditUser.Create());

        Assert.Throws<InvalidOperationException>(() => customer.MarkCreated());
    }

    [Fact]
    public void Block_Should_BlockTheCustomerAndRaiseBlocked()
    {
        var customer = FakeCustomer.Create().WithoutEvents();

        customer.Block();

        Assert.True(customer.IsBlocked);
        Assert.IsType<CustomerBlocked>(Assert.Single(customer.DomainEvents));
    }

    [Fact]
    public void RegisterCompletedOrder_Should_IncrementTheCount()
    {
        var customer = FakeCustomer.Create().WithCompletedOrderCount(2);

        customer.RegisterCompletedOrder();

        Assert.Equal(3, customer.CompletedOrderCount);
    }

    [Fact]
    public void RegisterCompletedOrder_WhenBlocked_Should_ThrowBlockedAndKeepTheCount()
    {
        var customer = FakeCustomer.Create().WithCompletedOrderCount(2).WithIsBlocked(true);

        var exception = Assert.Throws<BusinessRuleException>(() => customer.RegisterCompletedOrder());

        Assert.Equal(CustomerConstants.BlockedCode, exception.Code);
        Assert.Equal(2, customer.CompletedOrderCount);
    }
}
