using Boilerplate.Shared.Domain;
using System.Reflection;
using System.Text.Json;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string LastError { get; private set; }

    public static OutboxMessage From(EventBase @event)
    {
        return new OutboxMessage
        {
            Id = @event.EventId,
            Type = @event.GetType().FullName,
            Payload = JsonSerializer.Serialize(@event, @event.GetType()),
            OccurredAt = @event.OccurredAt,
            NextAttemptAt = @event.OccurredAt
        };
    }

    public EventBase ToEvent(Assembly eventAssembly)
    {
        return (EventBase)JsonSerializer.Deserialize(Payload, eventAssembly.GetType(Type, throwOnError: true));
    }
}
