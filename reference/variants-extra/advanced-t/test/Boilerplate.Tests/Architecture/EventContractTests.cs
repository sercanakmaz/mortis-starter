using Boilerplate.Tests.TestSupport.Architecture;

namespace Boilerplate.Tests.Architecture;

public class EventContractTests
{
    [Fact]
    public void EveryQueueEvent_Should_MatchItsPinnedContract()
    {
        var pinned = EventContracts.ReadPinned();

        var current = EventContracts.Describe();

        Assert.Equal(pinned, current);
    }
}
