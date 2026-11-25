using Acme.Shared.Domain;
using Acme.Tests.TestSupport.Shared.Domain;

namespace Acme.Tests.Shared.Domain;

public class EventHandlerExecutorTests
{
    [Fact]
    public async Task ExecuteRaisedAsync_WithTwoHandlers_Should_AwaitThemInSequenceOrder()
    {
        var handledBy = new List<string>();
        var eventContext = new EventContext();
        eventContext.AddRaised(new FakeEvent("first"));
        var eventHandlerExecutor = EventHandlerExecutorBuilder.Create()
            .WithEventContext(eventContext)
            .WithEventHandler(new RecordingEventHandler<FakeEvent>("second", 2, handledBy))
            .WithEventHandler(new RecordingEventHandler<FakeEvent>("first", 1, handledBy))
            .Build();

        await eventHandlerExecutor.ExecuteRaisedAsync();

        Assert.Equal(["first", "second"], handledBy);
    }

    [Fact]
    public async Task ExecuteRaisedAsync_WhenAHandlerRaisesAnotherEvent_Should_HandleThatEventToo()
    {
        var handledBy = new List<string>();
        var eventContext = new EventContext();
        eventContext.AddRaised(new FakeEvent("first"));
        var eventHandlerExecutor = EventHandlerExecutorBuilder.Create()
            .WithEventContext(eventContext)
            .WithEventHandler(new RecordingEventHandler<FakeEvent>("source", 0, handledBy, eventContext, new FakeCascadedEvent("cascaded")))
            .WithEventHandler(new RecordingEventHandler<FakeCascadedEvent>("cascaded", 0, handledBy))
            .Build();

        await eventHandlerExecutor.ExecuteRaisedAsync();

        Assert.Equal(["source", "cascaded"], handledBy);
    }

    [Fact]
    public async Task ExecuteRaisedAsync_Should_MoveHandledEventsToDispatchedInOrder()
    {
        var firstEvent = new FakeEvent("first");
        var secondEvent = new FakeEvent("second");
        var eventContext = new EventContext();
        eventContext.AddRaised(firstEvent);
        eventContext.AddRaised(secondEvent);
        var eventHandlerExecutor = EventHandlerExecutorBuilder.Create().WithEventContext(eventContext).Build();

        await eventHandlerExecutor.ExecuteRaisedAsync();

        Assert.Equal(0, eventContext.RaisedCount);
        Assert.Same(firstEvent, eventContext.TakeDispatched());
        Assert.Same(secondEvent, eventContext.TakeDispatched());
    }

    [Fact]
    public async Task ExecuteRaisedAsync_WhenAHandlerThrows_Should_Propagate()
    {
        var eventContext = new EventContext();
        eventContext.AddRaised(new FakeEvent("first"));
        var eventHandlerExecutor = EventHandlerExecutorBuilder.Create()
            .WithEventContext(eventContext)
            .WithEventHandler(new ThrowingEventHandler<FakeEvent>())
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => eventHandlerExecutor.ExecuteRaisedAsync());
    }
}
