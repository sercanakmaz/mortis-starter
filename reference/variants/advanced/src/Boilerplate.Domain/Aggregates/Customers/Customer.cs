using Boilerplate.Domain.Aggregates.Customers.Events;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Diagnostics;

namespace Boilerplate.Domain.Aggregates.Customers;

public class Customer : AggregateRoot<Guid>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Name { get; protected set; }
    public string Email { get; protected set; }
    public bool IsBlocked { get; protected set; }
    public int CompletedOrderCount { get; protected set; }
    public string Note { get; protected set; }
    public string TaxNumber { get; private set; }

    [DebuggerStepThrough]
    protected Customer() { }

    public Customer(Guid id, string name, string email, AuditUser user = null) : base(user)
    {
        Id = id;
        Name = name;
        Email = email.Trim().ToLowerInvariant();

        RaiseEvent(new Created(Id.ToString(), Email));
    }

    public void SetTaxNumber(string taxNumber)
    {
        TaxNumber = taxNumber;

        RaiseEvent(new TaxNumberSet(Id.ToString()));
    }

    public void Block()
    {
        IsBlocked = true;

        RaiseEvent(new Blocked(Id.ToString()));
    }

    public void Change(string name, string note)
    {
        Name = name;
        Note = note;

        RaiseEvent(new Changed(Id.ToString()));
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
