using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Shared.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Infrastructure.Diagnostics;

public class OutboxLagProbe(IServiceScopeFactory serviceScopeFactory, ILogger<OutboxLagProbe> logger) : BackgroundService
{
    private const int RefreshSeconds = 15;

    public int Pending { get; private set; }
    public double OldestAgeSeconds { get; private set; }

    public void Register()
    {
        Telemetry.Meter.CreateObservableGauge("outbox.pending", () => Pending, description: "Outbox rows not published yet.");
        Telemetry.Meter.CreateObservableGauge("outbox.oldest_age", () => OldestAgeSeconds, "s", "Age of the oldest outbox row not published yet.");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var outboxMessages = scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>().AsNoTracking();

        Pending = await outboxMessages.CountAsync(cancellationToken);
        var oldest = await outboxMessages.MinAsync(message => (DateTime?)message.OccurredAt, cancellationToken);
        OldestAgeSeconds = oldest is { } occurredAt ? (DateTime.UtcNow - occurredAt).TotalSeconds : 0;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Register();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Measuring the outbox failed; readiness reports it, and the probe tries again");
            }

            await Task.Delay(TimeSpan.FromSeconds(RefreshSeconds), stoppingToken);
        }
    }
}
