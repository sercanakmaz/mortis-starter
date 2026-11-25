using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Shared.Persistence;
using Microsoft.Extensions.Logging;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Messaging;

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
        var consumerName = eventConsumerEndpoint.ConsumerType.FullName;

        for (var attempt = 1; ; attempt++)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var eventConsumer = (IEventConsumer)scope.ServiceProvider.GetRequiredService(eventConsumerEndpoint.ConsumerType);

            try
            {
                await unitOfWork.BeginTransactionAsync(nameof(EventPublisher));
                await eventConsumer.Handle(@event);
                await unitOfWork.CommitTransactionAsync(nameof(EventPublisher));

                logger.LogInformation("Handled {Event} {EventId} by {EventConsumer}", @event.GetType().Name, @event.EventId, consumerName);

                return;
            }
            catch (Exception exception) when (attempt <= eventConsumerEndpoint.RetryCount)
            {
                await unitOfWork.RollbackTransactionAsync();

                logger.LogWarning(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}, attempt {Attempt}", consumerName, @event.GetType().Name, @event.EventId, attempt);

                await Task.Delay(TimeSpan.FromMilliseconds(attempt * MessagingConstants.RetryDelayMilliseconds));
            }
            catch (Exception exception)
            {
                await unitOfWork.RollbackTransactionAsync();

                logger.LogError(exception, "Event consumer {EventConsumer} failed for {Event} {EventId} after {Attempt} attempts; the transaction that raised the event stays committed", consumerName, @event.GetType().Name, @event.EventId, attempt);

                return;
            }
        }
    }
}
