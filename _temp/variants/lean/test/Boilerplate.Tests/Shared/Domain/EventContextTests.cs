using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.Shared.Domain;

public class EventContextTests
{
    [Fact]
    public void TakeRaised_Should_ReturnEventsInRaisedOrder()
    {
        var firstEvent = new FakeEvent("first");
        var secondEvent = new FakeEvent("second");
        var eventContext = new EventContext();

        eventContext.AddRaised(firstEvent);
        eventContext.AddRaised(secondEvent);

        Assert.Same(firstEvent, eventContext.TakeRaised());
        Assert.Same(secondEvent, eventContext.TakeRaised());
        Assert.Null(eventContext.TakeRaised());
    }

    [Fact]
    public void Clear_Should_DropRaisedAndDispatchedEvents()
    {
        var eventContext = new EventContext();
        eventContext.AddRaised(new FakeEvent("raised"));
        eventContext.AddDispatched(new FakeEvent("dispatched"));

        eventContext.Clear();

        Assert.Equal(0, eventContext.RaisedCount);
        Assert.Null(eventContext.TakeRaised());
        Assert.Null(eventContext.TakeDispatched());
    }

    [Fact]
    public void AddRaised_InsideACommand_Should_StampTheCommandContextOnTheEvent()
    {
        var commandContext = new CommandContext { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1", Language = "tr" };
        var eventContext = new EventContext();
        eventContext.EnterCommand(commandContext);

        eventContext.AddRaised(new FakeEvent("first"));

        var raisedEvent = eventContext.TakeRaised();
        Assert.Equal(commandContext.CorrelationId, raisedEvent.CorrelationId);
        Assert.Equal(commandContext.CausationId, raisedEvent.CausationId);
        Assert.Equal(["tenant-1", "tr"], [raisedEvent.TenantId, raisedEvent.Language]);
    }

    [Fact]
    public void LeaveCommand_Should_RestoreTheContextOfTheOuterCommand()
    {
        var outerContext = new CommandContext { CorrelationId = Guid.CreateVersion7() };
        var eventContext = new EventContext();
        eventContext.EnterCommand(outerContext);
        eventContext.EnterCommand(new CommandContext { CorrelationId = Guid.CreateVersion7() });

        eventContext.LeaveCommand();
        eventContext.AddRaised(new FakeEvent("after-nested"));

        Assert.Equal(outerContext.CorrelationId, eventContext.TakeRaised().CorrelationId);
    }
}
