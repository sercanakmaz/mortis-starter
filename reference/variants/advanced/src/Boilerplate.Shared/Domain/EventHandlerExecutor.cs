using Boilerplate.Shared.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Shared.Domain;

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
                using var activity = Telemetry.Source.StartActivity($"{eventHandler.GetType().Name} handles {@event.GetType().Name}");

                await eventHandler.Handle(@event);
            }

            eventContext.AddDispatched(@event);
        }
    }
}
