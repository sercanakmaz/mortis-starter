using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxRepository(AppDbContext context)
{
    public void Add(IEnumerable<EventBase> events)
    {
        context.Set<OutboxMessage>().AddRange(events.Select(OutboxMessage.From));
    }

    public async Task<OutboxMessage> ClaimNextAsync()
    {
        var messages = await context.Set<OutboxMessage>()
            .FromSql($"select * from outbox_messages where next_attempt_at <= {DateTime.UtcNow} order by occurred_at, id limit 1 for update skip locked")
            .ToListAsync();

        return messages.SingleOrDefault();
    }
    public void Remove(OutboxMessage message)
    {
        context.Set<OutboxMessage>().Remove(message);
    }

    public async Task RecordFailureAsync(OutboxMessage message, Exception exception, int maxBackoffSeconds)
    {
        context.ChangeTracker.Clear();

        await context.Set<OutboxMessage>().Where(row => row.Id == message.Id).ExecuteUpdateAsync(row => row
            .SetProperty(column => column.Attempts, message.Attempts + 1)
            .SetProperty(column => column.NextAttemptAt, DateTime.UtcNow.AddSeconds(Math.Min(Math.Pow(2, message.Attempts), maxBackoffSeconds)))
            .SetProperty(column => column.LastError, exception.Message));
    }
}
