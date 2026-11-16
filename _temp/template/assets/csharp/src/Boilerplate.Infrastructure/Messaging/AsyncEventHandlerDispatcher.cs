using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Messaging;

public class AsyncEventHandlerDispatcher(IServiceScopeFactory serviceScopeFactory, ILogger<AsyncEventHandlerDispatcher> logger) : IEventDispatcher
{
    public async Task Publish(EventBase @event)
    {
        if (@event is not IQueueEvent) return;

        using var scope = serviceScopeFactory.CreateScope();

        var eventHandlers = scope.ServiceProvider
            .GetServices(typeof(BaseAsyncEventHandler<>).MakeGenericType(@event.GetType()))
            .Cast<IAsyncEventHandler>()
            .OrderBy(eventHandler => eventHandler.Sequence)
            .ToList();

        if (eventHandlers.Count == 0) return;

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        foreach (var eventHandler in eventHandlers)
        {
            var transactionKey = $"{@event.EventId}:{eventHandler.GetType().Name}";

            for (var attempt = 1; attempt <= MessagingConstants.MaxAttempts; attempt++)
            {
                try
                {
                    await unitOfWork.BeginTransactionAsync(transactionKey);
                    await eventHandler.Handle(@event);
                    await unitOfWork.CommitTransactionAsync(transactionKey);

                    break;
                }
                catch (DbUpdateConcurrencyException) when (attempt < MessagingConstants.MaxAttempts)
                {
                    await unitOfWork.RollbackTransactionAsync();
                    await Task.Delay(TimeSpan.FromMilliseconds(attempt * MessagingConstants.RetryDelayMilliseconds));
                }
                catch (Exception exception)
                {
                    await unitOfWork.RollbackTransactionAsync();

                    logger.LogError(exception, "Async handler {EventHandler} failed for {EventType} {EventId}; the transaction that raised the event stays committed", eventHandler.GetType().Name, @event.GetType().FullName, @event.EventId);

                    break;
                }
            }
        }
    }
}
