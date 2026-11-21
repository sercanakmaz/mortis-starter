using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Boilerplate.Domain.Aggregates.Orders.Events;

namespace Boilerplate.Tests.Infrastructure.Messaging.RabbitMq;

public class MessageNamesTests
{
    [Fact]
    public void Exchange_Should_BeTheNamespaceAndTheTypeNameJoinedByAColon()
    {
        Assert.Equal("Boilerplate.Domain.Aggregates.Orders.Events:LineAdded", MessageNames.Exchange(typeof(LineAdded)));
    }

    [Fact]
    public void MessageTypes_Should_ListTheTypeItsBaseTypesAndItsOwnInterfacesAsUrnsLikeMassTransit()
    {
        Assert.Equal(
            ["urn:message:Boilerplate.Domain.Aggregates.Orders.Events:LineAdded", "urn:message:Boilerplate.Shared.Domain:EventBase", "urn:message:Boilerplate.Shared.Domain:IQueueEvent"],
            MessageNames.MessageTypes(typeof(LineAdded)));
    }
}
