using Boilerplate.Tests.TestSupport.Infrastructure.Messaging;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.Infrastructure.Messaging;

public class EventPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithTwoHandlers_Should_RunEachInItsOwnTransactionInSequenceOrder()
    {
        var calls = new List<string>();
        var eventPublisher = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("second", 2, calls))
            .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("first", 1, calls))
            .Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Equal(["BeginTransactionAsync", "first", "CommitTransactionAsync", "BeginTransactionAsync", "second", "CommitTransactionAsync"], calls);
    }

    [Fact]
    public async Task PublishAsync_WhenAHandlerThrows_Should_RollBackLogAndStillRunTheNextHandler()
    {
        var calls = new List<string>();
        var eventPublisherBuilder = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("failing", 1, calls, 1, new InvalidOperationException("boom")))
            .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("healthy", 2, calls));
        var eventPublisher = eventPublisherBuilder.Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Equal(["BeginTransactionAsync", "failing", "RollbackTransactionAsync", "BeginTransactionAsync", "healthy", "CommitTransactionAsync"], calls);
        var entry = Assert.Single(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task PublishAsync_WhenTheHandlerHitsAConcurrencyConflict_Should_RetryInAFreshTransaction()
    {
        var calls = new List<string>();
        var eventPublisherBuilder = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("contended", 0, calls, 2, new DbUpdateConcurrencyException("stale")));
        var eventPublisher = eventPublisherBuilder.Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Equal(
            ["BeginTransactionAsync", "contended", "RollbackTransactionAsync", "BeginTransactionAsync", "contended", "RollbackTransactionAsync", "BeginTransactionAsync", "contended", "CommitTransactionAsync"],
            calls);
        Assert.DoesNotContain(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task PublishAsync_WhenTheConflictNeverEnds_Should_GiveUpAfterTheMaximumAttemptsAndLog()
    {
        var calls = new List<string>();
        var eventPublisherBuilder = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("contended", 0, calls, int.MaxValue, new DbUpdateConcurrencyException("stale")));
        var eventPublisher = eventPublisherBuilder.Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Equal(MessagingConstants.MaxAttempts, calls.Count(call => call == "contended"));
        Assert.Equal(MessagingConstants.MaxAttempts, calls.Count(call => call == "RollbackTransactionAsync"));
        Assert.Single(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task PublishAsync_WithoutHandlersForTheEvent_Should_NotOpenATransaction()
    {
        var calls = new List<string>();
        var eventPublisher = EventPublisherBuilder.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();

        await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

        Assert.Empty(calls);
    }
}
