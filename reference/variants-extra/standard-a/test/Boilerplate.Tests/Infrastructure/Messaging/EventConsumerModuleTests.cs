using Boilerplate.Tests.TestSupport.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Orders.Events;

namespace Boilerplate.Tests.Infrastructure.Messaging;

public class EventConsumerModuleTests
{
    [Fact]
    public void AddEventConsumer_Should_AddOneEndpointPerConsumerAndRegisterTheConsumer()
    {
        var services = new ServiceCollection();

        services.AddEventConsumerRegistry()
            .AddEventConsumer<RecordingEventConsumer>(endpoint => endpoint.Retry(1))
            .AddEventConsumer<OtherRecordingEventConsumer>(endpoint => endpoint.Retry(5));

        var eventConsumerRegistry = Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<EventConsumerRegistry>());
        Assert.Equal([1, 5], eventConsumerRegistry.For(typeof(FakeQueueEvent)).Select(endpoint => endpoint.RetryCount));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OtherRecordingEventConsumer) && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Endpoint_Should_TakeTheEventFromTheConsumerAndItsQueueFromTheConsumerName()
    {
        var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(DecreaseStockWhenOrderLineAddedEventConsumer));

        Assert.Equal(typeof(LineAdded), eventConsumerEndpoint.EventType);
        Assert.Equal("decrease-stock-when-order-line-added", eventConsumerEndpoint.Queue);
        Assert.Equal(3, eventConsumerEndpoint.RetryCount);
    }

    [Fact]
    public void Named_And_Retry_Should_OverrideTheQueueAndTheRetryCountInCode()
    {
        var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(DecreaseStockWhenOrderLineAddedEventConsumer)).Named("stock-decrease").Retry(5);

        Assert.Equal(["stock-decrease", "5"], [eventConsumerEndpoint.Queue, eventConsumerEndpoint.RetryCount.ToString()]);
    }
}
