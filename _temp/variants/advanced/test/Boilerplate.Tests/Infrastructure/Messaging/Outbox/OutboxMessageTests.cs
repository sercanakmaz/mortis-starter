using System.Diagnostics;
using Boilerplate.Tests.TestSupport.Shared.Diagnostics;
using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Infrastructure.Messaging.Outbox;

namespace Boilerplate.Tests.Infrastructure.Messaging.Outbox;

public class OutboxMessageTests
{
    [Fact]
    public void From_Should_KeepTheEventIdAndTheTenantAndMakeTheRowDueAtOnce()
    {
        var fakeQueueEvent = new FakeQueueEvent("first") { TenantId = "tenant-1" };

        var outboxMessage = OutboxMessage.From(fakeQueueEvent);

        Assert.Equal(fakeQueueEvent.EventId, outboxMessage.Id);
        Assert.Equal("tenant-1", outboxMessage.TenantId);
        Assert.Equal(fakeQueueEvent.OccurredAt, outboxMessage.NextAttemptAt);
        Assert.Equal(0, outboxMessage.Attempts);
    }

    [Fact]
    public void ToEvent_Should_ReadBackTheSameEventWithItsContext()
    {
        var fakeQueueEvent = new FakeQueueEvent("first") { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1", UserId = "user-1" };

        var readBack = OutboxMessage.From(fakeQueueEvent).ToEvent(typeof(FakeQueueEvent).Assembly);

        Assert.Equal(fakeQueueEvent, readBack);
    }

    [Fact]
    public void From_InsideAnActivity_Should_StoreThatActivitysIdAsTraceParent()
    {
        using var recordedActivities = new RecordedActivities();
        using var activity = recordedActivities.TestSource.StartActivity("request");

        var outboxMessage = OutboxMessage.From(new FakeQueueEvent("first"));

        Assert.Equal(activity!.Id, outboxMessage.TraceParent);
    }

    [Fact]
    public void From_OutsideAnyActivity_Should_StoreNoTraceParent()
    {
        Activity.Current = null;

        var outboxMessage = OutboxMessage.From(new FakeQueueEvent("first"));

        Assert.Null(outboxMessage.TraceParent);
    }

}
