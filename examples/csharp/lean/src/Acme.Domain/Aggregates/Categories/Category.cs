using System.Diagnostics;
using Acme.Domain.Aggregates.Categories.Events;
using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Categories;

public class Category : AggregateRoot<int>
{
    public string Name { get; protected set; }

    [DebuggerStepThrough]
    protected Category() { }

    public Category(string name, AuditUser user = null) : base(user)
    {
        Name = name.Trim();
    }

    public void MarkCreated()
    {
        if (Id == 0)
        {
            throw new InvalidOperationException("A category is marked created only after the database has assigned its id.");
        }

        RaiseEvent(new Created(Id.ToString(), Name));
    }
}
