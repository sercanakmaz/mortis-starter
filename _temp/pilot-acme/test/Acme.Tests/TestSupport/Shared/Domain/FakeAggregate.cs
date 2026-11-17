using Acme.Shared.Domain;

namespace Acme.Tests.TestSupport.Shared.Domain;

public class FakeAggregate : AggregateRoot<Guid>
{
    private FakeAggregate(Guid id, AuditUser user) : base(user)
    {
        Id = id;
    }

    private FakeAggregate() { }

    public static FakeAggregate Create(Guid id) => new(id, FakeAuditUser.Create());

    public static FakeAggregate Create() => Create(Guid.CreateVersion7());

    public static FakeAggregate CreateEmpty() => new();

    public FakeAggregate WithRaisedEvent(EventBase @event)
    {
        RaiseEvent(@event);
        return this;
    }

    public FakeAggregate WithUpdatedAt(DateTime updatedAt)
    {
        UpdatedAt = updatedAt;
        return this;
    }
}
