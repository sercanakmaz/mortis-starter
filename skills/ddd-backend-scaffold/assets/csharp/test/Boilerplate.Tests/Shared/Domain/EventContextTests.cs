using Boilerplate.Tests.TestSupport.Shared.Domain;
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
}
