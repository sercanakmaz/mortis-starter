using System.Runtime.CompilerServices;
using Boilerplate.Contracts;
using Boilerplate.Domain;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.Architecture;

public class ContractsRulesTests
{
    [Fact]
    public void ContractsProject_Should_HoldOnlyQueueEvents()
    {
        var offenders = typeof(ContractsMarker).Assembly.GetTypes()
            .Where(type => type != typeof(ContractsMarker) && !typeof(IQueueEvent).IsAssignableFrom(type) && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryQueueEvent_Should_LiveInTheContractsProject()
    {
        var offenders = typeof(DomainModule).Assembly.GetTypes().Where(type => typeof(IQueueEvent).IsAssignableFrom(type) && !type.IsInterface).Select(type => type.Name);

        Assert.Empty(offenders);
    }
}
