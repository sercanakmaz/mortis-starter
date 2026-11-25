using Boilerplate.Contracts.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Infrastructure.ReadModels;
using Boilerplate.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Tests.Infrastructure.ReadModels;

public class ReadModelModuleTests
{
    [Fact]
    public void AddReadModelModule_Should_RefreshTheSummaryOnEveryOrderEvent()
    {
        var services = new ServiceCollection().AddReadModelModule();

        Assert.All([typeof(Created), typeof(LineAdded), typeof(LineCancelled), typeof(Completed), typeof(Cancelled)],
            eventType => Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(BaseSyncEventHandler<>).MakeGenericType(eventType)));
    }
}
