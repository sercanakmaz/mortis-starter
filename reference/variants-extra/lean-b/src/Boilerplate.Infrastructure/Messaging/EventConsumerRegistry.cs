namespace Boilerplate.Infrastructure.Messaging;

public class EventConsumerRegistry
{
    public List<EventConsumerEndpoint> Endpoints { get; } = [];

    public IEnumerable<EventConsumerEndpoint> For(Type eventType) => Endpoints.Where(endpoint => endpoint.EventType == eventType);

    public EventConsumerEndpoint ByConsumer(string consumerName) => Endpoints.Single(endpoint => endpoint.ConsumerType.FullName == consumerName);
}
