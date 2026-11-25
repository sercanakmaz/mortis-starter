using System.ComponentModel.DataAnnotations.Schema;

namespace Boilerplate.Shared.Domain;

public abstract class AggregateRoot<TKey> : Entity<TKey>
{
    private List<EventBase> _domainEvents;

    public DateTime CreatedAt { get; protected set; }
    public string CreatedUserId { get; protected set; }
    public string CreatedUserName { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }
    public string UpdatedUserId { get; protected set; }
    public string UpdatedUserName { get; protected set; }

    [NotMapped] public long Version => UpdatedAt.Ticks / TimeSpan.TicksPerMicrosecond;

    [NotMapped] public IReadOnlyCollection<EventBase> DomainEvents => _domainEvents?.AsReadOnly();

    protected AggregateRoot()
    {
        var currentDate = DateTime.UtcNow;
        CreatedAt = currentDate;
        UpdatedAt = currentDate;
    }

    protected AggregateRoot(AuditUser user)
    {
        var currentDate = DateTime.UtcNow;
        CreatedAt = currentDate;
        CreatedUserId = user?.Id;
        CreatedUserName = user?.Email;
        UpdatedAt = currentDate;
        UpdatedUserId = user?.Id;
        UpdatedUserName = user?.Email;
    }

    public void SetUpdatedAudit(AuditUser user)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedUserId = user?.Id;
        UpdatedUserName = user?.Email;

        for (var index = 0; index < (_domainEvents?.Count ?? 0); index++)
        {
            _domainEvents[index] = _domainEvents[index] with { UserId = UpdatedUserId, UserEmail = UpdatedUserName };
        }
    }

    public void ClearEvents() => _domainEvents?.Clear();

    protected void RaiseEvent(EventBase @event)
    {
        _domainEvents ??= [];
        _domainEvents.Add(@event with { UserId = UpdatedUserId, UserEmail = UpdatedUserName });
    }
}
