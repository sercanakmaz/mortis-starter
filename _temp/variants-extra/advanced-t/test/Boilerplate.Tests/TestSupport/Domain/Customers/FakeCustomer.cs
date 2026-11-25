using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Customers;

namespace Boilerplate.Tests.TestSupport.Domain.Customers;

public class FakeCustomer : Customer
{
    private FakeCustomer(Guid id) : base(id, "Default Customer", "default-customer@example.com", FakeAuditUser.Create())
    {
    }

    private FakeCustomer() { }

    public static FakeCustomer Create(Guid id) => new(id);
    public static FakeCustomer Create() => Create(Guid.CreateVersion7());
    public static FakeCustomer CreateEmpty() => new();

    public FakeCustomer WithName(string name)
    {
        Name = name;
        return this;
    }

    public FakeCustomer WithEmail(string email)
    {
        Email = email;
        return this;
    }

    public FakeCustomer WithIsBlocked(bool isBlocked)
    {
        IsBlocked = isBlocked;
        return this;
    }

    public FakeCustomer WithCompletedOrderCount(int completedOrderCount)
    {
        CompletedOrderCount = completedOrderCount;
        return this;
    }

    public FakeCustomer WithoutEvents()
    {
        ClearEvents();
        return this;
    }
}
