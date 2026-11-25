using System.Diagnostics;
using Acme.Domain.Aggregates.Customers.Events;
using Acme.Shared.Domain;
using Acme.Shared.Exceptions;

namespace Acme.Domain.Aggregates.Customers;

public class Customer : AggregateRoot<Guid>
{
    public string Name { get; protected set; }

    public string Email { get; protected set; }

    public bool IsBlocked { get; protected set; }

    public int CompletedOrderCount { get; protected set; }

    public string Note { get; protected set; }

    [DebuggerStepThrough]
    protected Customer() { }

    public Customer(Guid id, string name, string email, AuditUser user = null) : base(user)
    {
        Id = id;
        Name = name;
        Email = email.Trim().ToLowerInvariant();

        RaiseEvent(new Created(Id.ToString(), Email));
    }

    public void Block()
    {
        IsBlocked = true;

        RaiseEvent(new Blocked(Id.ToString()));
    }

    public void RegisterCompletedOrder()
    {
        if (IsBlocked)
        {
            throw new BusinessRuleException(CustomerConstants.BlockedCode, "A blocked customer cannot complete orders.");
        }

        CompletedOrderCount++;
    }

    public void Change(string name, string note)
    {
        Name = name;
        Note = note;

        RaiseEvent(new Changed(Id.ToString()));
    }
}
