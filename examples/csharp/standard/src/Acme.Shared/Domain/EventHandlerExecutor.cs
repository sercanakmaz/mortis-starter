using Microsoft.Extensions.DependencyInjection;

namespace Acme.Shared.Domain;

public sealed class EventHandlerExecutor(IServiceProvider serviceProvider, EventContext eventContext)
{
    public async Task ExecuteRaisedAsync()
    {
        while (eventContext.TakeRaised() is { } @event)
        {
            var eventHandlers = serviceProvider
                .GetServices(typeof(BaseSyncEventHandler<>).MakeGenericType(@event.GetType()))
                .Cast<ISyncEventHandler>()
                .OrderBy(eventHandler => eventHandler.Sequence)
                .ToList();

            foreach (var eventHandler in eventHandlers)
            {
                await eventHandler.Handle(@event);
            }

            eventContext.AddDispatched(@event);
        }
    }
}
