using Boilerplate.Domain.Aggregates.Customers.Events;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Diagnostics;

namespace Boilerplate.Domain.Aggregates.Customers;

public class Customer : AggregateRoot<int>
{
    public string Name { get; protected set; }
    public string Email { get; protected set; }
    public bool IsBlocked { get; protected set; }
    public int CompletedOrderCount { get; protected set; }

    [DebuggerStepThrough]
    protected Customer() { }

    public Customer(string name, string email, AuditUser user = null) : base(user)
    {
        Name = name;
        Email = email.Trim().ToLowerInvariant();
    }

    public void MarkCreated()
    {
        if (Id == 0)
        {
            throw new InvalidOperationException("A customer is marked created only after the database has assigned its id.");
        }

        RaiseEvent(new CustomerCreated(Id.ToString(), Email));
    }

    public void Block()
    {
        IsBlocked = true;

        RaiseEvent(new CustomerBlocked(Id.ToString()));
    }

    public void RegisterCompletedOrder()
    {
        if (IsBlocked)
        {
            throw new BusinessRuleException(CustomerConstants.BlockedCode, "A blocked customer cannot complete orders.");
        }

        CompletedOrderCount++;
    }
}
