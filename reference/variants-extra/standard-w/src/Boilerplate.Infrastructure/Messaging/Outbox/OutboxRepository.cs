using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxRepository(AppDbContext context, EventConsumerRegistry eventConsumerRegistry)
{
    public void Add(IEnumerable<EventBase> events)
    {
        foreach (var @event in events)
        {
            context.Set<OutboxMessage>().AddRange(eventConsumerRegistry.For(@event.GetType()).Select(eventConsumerEndpoint => OutboxMessage.From(@event, eventConsumerEndpoint)));
        }
    }

    public async Task<OutboxMessage> ClaimNextAsync()
    {
        var messages = await context.Set<OutboxMessage>()
            .FromSql($"select * from outbox_messages where attempts < max_attempts and next_attempt_at <= {DateTime.UtcNow} order by occurred_at, id limit 1 for update skip locked")
            .ToListAsync();

        return messages.SingleOrDefault();
    }

    public void Remove(OutboxMessage message)
    {
        context.Set<OutboxMessage>().Remove(message);
    }

    public async Task RecordFailureAsync(OutboxMessage message, Exception exception)
    {
        context.ChangeTracker.Clear();

        await context.Set<OutboxMessage>().Where(row => row.Id == message.Id).ExecuteUpdateAsync(row => row
            .SetProperty(column => column.Attempts, message.Attempts + 1)
            .SetProperty(column => column.NextAttemptAt, DateTime.UtcNow.AddSeconds(Math.Pow(2, message.Attempts)))
            .SetProperty(column => column.LastError, exception.Message));
    }
}
