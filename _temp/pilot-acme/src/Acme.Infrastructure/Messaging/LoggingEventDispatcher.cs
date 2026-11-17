using Acme.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace Acme.Infrastructure.Messaging;

public class LoggingEventDispatcher(ILogger<LoggingEventDispatcher> logger) : IEventDispatcher
{
    public Task Publish(EventBase @event)
    {
        var eventType = @event.GetType();

        logger.LogInformation("Published {Exchange} {EventId}", $"{eventType.Namespace}:{eventType.Name}", @event.EventId);

        return Task.CompletedTask;
    }
}
