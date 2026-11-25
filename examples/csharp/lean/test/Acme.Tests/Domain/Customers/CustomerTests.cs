using Acme.Domain.Aggregates.Customers;
using Acme.Domain.Aggregates.Customers.Events;
using Acme.Shared.Exceptions;
using Acme.Tests.TestSupport.Domain.Customers;
using Acme.Tests.TestSupport.Shared.Domain;

namespace Acme.Tests.Domain.Customers;

public class CustomerTests
{
    [Fact]
    public void Empty_Constructor_Should_Create()
    {
        Assert.NotNull(FakeCustomer.CreateEmpty());
    }

    [Fact]
    public void Constructor_Should_NormalizeTheEmailAndRaiseCreated()
    {
        var id = Guid.CreateVersion7();

        var customer = new Customer(id, "Ada", "  Ada@Example.COM ", FakeAuditUser.Create());

        Assert.Equal("ada@example.com", customer.Email);
        var created = Assert.IsType<Created>(Assert.Single(customer.DomainEvents));
        Assert.Equal(id.ToString(), created.Id);
        Assert.Equal("ada@example.com", created.Email);
    }

    [Fact]
    public void Block_Should_BlockTheCustomerAndRaiseBlocked()
    {
        var customer = FakeCustomer.Create().WithoutEvents();

        customer.Block();

        Assert.True(customer.IsBlocked);
        Assert.IsType<Blocked>(Assert.Single(customer.DomainEvents));
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
