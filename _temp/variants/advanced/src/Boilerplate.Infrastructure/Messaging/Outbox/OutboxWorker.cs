using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Boilerplate.Shared.Persistence;
using Boilerplate.Domain;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxWorker(IServiceScopeFactory serviceScopeFactory, RabbitMqPublisher rabbitMqPublisher, IOptions<OutboxSettings> settings, ILogger<OutboxWorker> logger) : BackgroundService
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
            await rabbitMqPublisher.PublishAsync(@event, CancellationToken.None);

            outboxRepository.Remove(message);

            await unitOfWork.CommitTransactionAsync(nameof(OutboxWorker));

            logger.LogInformation("Published {Event} {EventId}", @event.GetType().Name, @event.EventId);
        }
        catch (Exception exception)
        {
            await unitOfWork.RollbackTransactionAsync();
            await outboxRepository.RecordFailureAsync(message, exception, settings.Value.MaxBackoffSeconds);

            logger.LogWarning(exception, "Publishing {Event} {EventId} to RabbitMQ failed, attempt {Attempt}", @event.GetType().Name, @event.EventId, message.Attempts + 1);
        }

        return true;
    }
}
