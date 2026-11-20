namespace Boilerplate.Shared.Domain;

public abstract record EventBase
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public Guid CorrelationId { get; init; }
    public Guid? CausationId { get; init; }
    public string TenantId { get; init; }
    public string Language { get; init; }
    public string UserId { get; init; }
    public string UserEmail { get; init; }
}

public interface IQueueEvent;
