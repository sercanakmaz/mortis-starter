using System.Diagnostics;

namespace Acme.Shared.Domain;

public abstract record EventBase
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public Guid CorrelationId { get; init; } = Trace.CorrelationManager.ActivityId;

    public string UserId { get; init; }

    public string UserEmail { get; init; }
}

public interface IQueueEvent;
