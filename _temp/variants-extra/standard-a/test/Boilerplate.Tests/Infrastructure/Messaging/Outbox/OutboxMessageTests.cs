using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Messaging;

namespace Boilerplate.Tests.Infrastructure.Messaging.Outbox;

public class OutboxMessageTests
{
    [Fact]
    public void From_Should_MakeOneRowForTheEventAndTheConsumerDueAtOnceWithItsAttemptLimit()
    {
        var fakeQueueEvent = new FakeQueueEvent("first");

        var outboxMessage = OutboxMessage.From(fakeQueueEvent, new EventConsumerEndpoint(typeof(RecordingEventConsumer)).Retry(4));

        Assert.Equal(fakeQueueEvent.EventId, outboxMessage.EventId);
        Assert.NotEqual(fakeQueueEvent.EventId, outboxMessage.Id);
        Assert.Equal(typeof(RecordingEventConsumer).FullName, outboxMessage.Consumer);
        Assert.Equal(5, outboxMessage.MaxAttempts);
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
