using Boilerplate.Tests.TestSupport.Shared.Domain;

namespace Boilerplate.Tests.Shared.Domain;

public class AggregateRootTests
{
    [Fact]
    public void Constructor_Should_SetAuditFromTheUser()
    {
        var fakeAggregate = FakeAggregate.Create();

        Assert.Equal(FakeAuditUser.Id, fakeAggregate.CreatedUserId);
        Assert.Equal(FakeAuditUser.Email, fakeAggregate.CreatedUserName);
        Assert.Equal(DateTimeKind.Utc, fakeAggregate.CreatedAt.Kind);
        Assert.Equal(fakeAggregate.CreatedAt, fakeAggregate.UpdatedAt);
    }

    [Fact]
    public void RaiseEvent_Should_KeepEventsInRaisedOrder()
    {
        var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first")).WithRaisedEvent(new FakeEvent("second"));

        Assert.Equal(["first", "second"], fakeAggregate.DomainEvents.Cast<FakeEvent>().Select(raisedEvent => raisedEvent.Id));
    }

    [Fact]
    public void ClearEvents_Should_RemoveEveryRaisedEvent()
    {
        var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));

        fakeAggregate.ClearEvents();

        Assert.Empty(fakeAggregate.DomainEvents);
    }

    [Fact]
    public void SetUpdatedAudit_Should_MoveUpdatedAtForwardAndKeepCreatedAt()
    {
        var createdAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fakeAggregate = FakeAggregate.Create().WithUpdatedAt(createdAt);

        fakeAggregate.SetUpdatedAudit(FakeAuditUser.Create());

        Assert.True(fakeAggregate.UpdatedAt > createdAt);
        Assert.Equal("test-user-id", fakeAggregate.UpdatedUserId);
    }

    [Fact]
    public void RaiseEvent_Should_StampTheEventWithTheCurrentAuditUser()
    {
        var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));

        var raisedEvent = Assert.Single(fakeAggregate.DomainEvents);

        Assert.Equal([FakeAuditUser.Id, FakeAuditUser.Email], [raisedEvent.UserId, raisedEvent.UserEmail]);
    }

    [Fact]
    public void SetUpdatedAudit_Should_RestampEventsRaisedBeforeIt()
    {
        var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));
        var other = FakeAuditUser.CreateOther();

        fakeAggregate.SetUpdatedAudit(other);

        var raisedEvent = Assert.Single(fakeAggregate.DomainEvents);

        Assert.Equal([other.Id, other.Email], [raisedEvent.UserId, raisedEvent.UserEmail]);
    }

    [Fact]
    public void SetUpdatedAudit_Should_KeepTheEventIdentityWhenRestamping()
    {
        var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));
        var eventId = Assert.Single(fakeAggregate.DomainEvents).EventId;

        fakeAggregate.SetUpdatedAudit(FakeAuditUser.CreateOther());

        Assert.Equal(eventId, Assert.Single(fakeAggregate.DomainEvents).EventId);
    }

    [Fact]
    public void Version_WithSameMicrosecond_Should_IgnoreSmallerTicks()
    {
        var updatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var storedAggregate = FakeAggregate.Create().WithUpdatedAt(updatedAt);
        var inMemoryAggregate = FakeAggregate.Create().WithUpdatedAt(updatedAt.AddTicks(7));

        Assert.Equal(storedAggregate.Version, inMemoryAggregate.Version);
    }
}
