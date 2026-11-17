using Microsoft.Extensions.Logging;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Messaging;

public class LoggingEventDispatcher(ILogger<LoggingEventDispatcher> logger) : IEventDispatcher
{
    public Task Publish(EventBase @event)
    {
        var eventType = @event.GetType();

        logger.LogInformation("Published {Exchange} {EventId} correlation {CorrelationId} causation {CausationId}", $"{eventType.Namespace}:{eventType.Name}", @event.EventId, @event.CorrelationId, @event.CausationId);

        return Task.CompletedTask;
    }
}
