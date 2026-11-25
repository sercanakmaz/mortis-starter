using Microsoft.Extensions.Logging;
using Acme.Tests.TestSupport.Infrastructure.Messaging;
using Acme.Tests.TestSupport.Shared.Domain;
using Acme.Tests.TestSupport.Shared.Persistence;

namespace Acme.Tests.Infrastructure.Messaging;

public class EventPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithTwoConsumers_Should_RunEachInItsOwnTransactionInRegistrationOrder()
    {
        var calls = new List<string>();
        var eventPublisher = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .WithEventConsumer(new OtherRecordingEventConsumer(calls), 0)
            .WithEventConsumer(new RecordingEventConsumer(calls), 0)
            .Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Equal(["BeginTransactionAsync", "OtherRecordingEventConsumer", "CommitTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "CommitTransactionAsync"], calls);
    }

    [Fact]
    public async Task PublishAsync_WhenAConsumerFails_Should_RetryItInAFreshTransactionUpToItsRetryCount()
    {
        var calls = new List<string>();
        var eventPublisherBuilder = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .WithEventConsumer(new RecordingEventConsumer(calls, 2, new InvalidOperationException("boom")), 2);
        var eventPublisher = eventPublisherBuilder.Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Equal(
            ["BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "CommitTransactionAsync"],
            calls);
        Assert.DoesNotContain(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task PublishAsync_WhenAConsumerKeepsFailing_Should_GiveUpAfterItsRetriesLogAndStillRunTheNextConsumer()
    {
        var calls = new List<string>();
        var eventPublisherBuilder = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .WithEventConsumer(new RecordingEventConsumer(calls, int.MaxValue, new InvalidOperationException("boom")), 1)
            .WithEventConsumer(new OtherRecordingEventConsumer(calls), 0);
        var eventPublisher = eventPublisherBuilder.Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Equal(
            ["BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "OtherRecordingEventConsumer", "CommitTransactionAsync"],
            calls);
        var entry = Assert.Single(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task PublishAsync_WithoutConsumersForTheEvent_Should_NotOpenATransaction()
    {
        var calls = new List<string>();
        var eventPublisher = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Empty(calls);
    }
}
