using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Domain;
using Boilerplate.Domain;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxWorker(IServiceScopeFactory serviceScopeFactory, EventConsumerRegistry eventConsumerRegistry, IOptions<OutboxSettings> settings, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await PublishAsync(outboxRepository => outboxRepository.ClaimNextAsync()))
                {
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "The outbox worker failed; it tries again after the polling interval");
            }

            await Task.Delay(settings.Value.PollingIntervalMilliseconds, stoppingToken);
        }
    }

    public async Task PublishAsync(Guid eventId)
    {
        while (await PublishAsync(outboxRepository => outboxRepository.ClaimAsync(eventId)))
        {
        }
    }
    private async Task<bool> PublishAsync(Func<OutboxRepository, Task<OutboxMessage>> claim)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<OutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await unitOfWork.BeginTransactionAsync(nameof(OutboxWorker));

        var message = await claim(outboxRepository);

        if (message is null)
        {
            await unitOfWork.RollbackTransactionAsync();

            return false;
        }

        var @event = message.ToEvent(typeof(DomainModule).Assembly);

        using var logScope = logger.BeginScope(LogFields.Of(@event));

        try
        {
            var eventConsumer = (IEventConsumer)scope.ServiceProvider.GetRequiredService(eventConsumerRegistry.ByConsumer(message.Consumer).ConsumerType);

            await eventConsumer.Handle(@event);

            outboxRepository.Remove(message);

            await unitOfWork.CommitTransactionAsync(nameof(OutboxWorker));

            logger.LogInformation("Handled {Event} {EventId} by {EventConsumer}", @event.GetType().Name, @event.EventId, message.Consumer);
        }
        catch (Exception exception)
        {
            await unitOfWork.RollbackTransactionAsync();
            await outboxRepository.RecordFailureAsync(message, exception);

            logger.LogWarning(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}, attempt {Attempt}", message.Consumer, @event.GetType().Name, @event.EventId, message.Attempts + 1);
        }

        return true;
    }
}
