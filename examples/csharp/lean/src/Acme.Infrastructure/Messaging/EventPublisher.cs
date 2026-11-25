using Acme.Infrastructure.Logging;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acme.Infrastructure.Messaging;

public class EventPublisher(IServiceScopeFactory serviceScopeFactory, EventConsumerRegistry eventConsumerRegistry, ILogger<EventPublisher> logger)
{
    public async Task PublishAsync(EventBase @event)
    {
        using var logScope = logger.BeginScope(LogFields.Of(@event));

        logger.LogInformation("Published {Event} {EventId}", @event.GetType().Name, @event.EventId);

        foreach (var eventConsumerEndpoint in eventConsumerRegistry.For(@event.GetType()))
        {
            await ConsumeAsync(eventConsumerEndpoint, @event);
        }
    }

    private async Task ConsumeAsync(EventConsumerEndpoint eventConsumerEndpoint, EventBase @event)
    {
        var attempts = eventConsumerEndpoint.RetryCount + 1;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var serviceScope = serviceScopeFactory.CreateScope();

            var unitOfWork = serviceScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var transactionKey = eventConsumerEndpoint.ConsumerType.FullName;

            try
            {
                await unitOfWork.BeginTransactionAsync(transactionKey);

                var eventConsumer = (IEventConsumer)serviceScope.ServiceProvider.GetRequiredService(eventConsumerEndpoint.ConsumerType);

                await eventConsumer.Handle(@event);

                await unitOfWork.CommitTransactionAsync(transactionKey);

                return;
            }
            catch (Exception exception)
            {
                await unitOfWork.RollbackTransactionAsync();

                if (attempt == attempts)
                {
                    logger.LogError(exception, "{Consumer} failed for {Event} {EventId} after {Attempts} attempts", eventConsumerEndpoint.ConsumerType.Name, @event.GetType().Name, @event.EventId, attempts);
                }
                else
                {
                    await Task.Delay(MessagingConstants.RetryDelayMilliseconds);
                }
            }
        }
    }
}
