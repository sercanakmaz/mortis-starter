using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Messaging;

public class EventPublisher(IServiceScopeFactory serviceScopeFactory, ILogger<EventPublisher> logger)
{
    public async Task PublishAsync(EventBase @event)
    {
        using var logScope = logger.BeginScope(LogFields.Of(@event));
        using var scope = serviceScopeFactory.CreateScope();

        logger.LogInformation("Published {Event} {EventId}", @event.GetType().Name, @event.EventId);

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

                    logger.LogError(exception, "Async handler {EventHandler} failed; the transaction that raised the event stays committed", eventHandler.GetType().Name);

                    break;
                }
            }
        }
    }
}
