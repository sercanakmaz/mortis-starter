using Acme.Infrastructure.Messaging;
using Acme.Infrastructure.Messaging.Outbox;
using Acme.Tests.TestSupport.Shared.Domain;

namespace Acme.Tests.Infrastructure.Messaging.Outbox;

public class OutboxMessageTests
{
    [Fact]
    public void From_Should_KeepTheEventIdAndTheConsumerAndMakeTheRowDueAtOnce()
    {
        var fakeQueueEvent = new FakeQueueEvent("first") { TenantId = "tenant-1" };

        var outboxMessage = OutboxMessage.From(fakeQueueEvent, new EventConsumerEndpoint(typeof(RecordingEventConsumer)).Retry(2));

        Assert.Equal(fakeQueueEvent.EventId, outboxMessage.EventId);
        Assert.Equal(typeof(RecordingEventConsumer).FullName, outboxMessage.Consumer);
        Assert.Equal(3, outboxMessage.MaxAttempts);
        Assert.Equal(fakeQueueEvent.OccurredAt, outboxMessage.NextAttemptAt);
        Assert.Equal(0, outboxMessage.Attempts);
    }

    [Fact]
    public void ToEvent_Should_ReadBackTheSameEventWithItsContext()
    {
        var fakeQueueEvent = new FakeQueueEvent("first") { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1", UserId = "user-1" };

        var readBack = OutboxMessage.From(fakeQueueEvent, new EventConsumerEndpoint(typeof(RecordingEventConsumer))).ToEvent(typeof(FakeQueueEvent).Assembly);

        Assert.Equal(fakeQueueEvent, readBack);
    }
}
