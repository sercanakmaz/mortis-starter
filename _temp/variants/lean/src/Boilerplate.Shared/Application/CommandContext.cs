using Boilerplate.Shared.Domain;

namespace Boilerplate.Shared.Application;

public sealed record CommandContext
{
    public AuditUser User { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid? CausationId { get; init; }
    public string TenantId { get; init; }
    public string Language { get; init; }

    public static CommandContext Complete(CommandContext commandContext)
    {
        var completed = commandContext ?? new CommandContext();

        return completed.CorrelationId == Guid.Empty ? completed with { CorrelationId = Guid.CreateVersion7() } : completed;
    }

    public static CommandContext FromEvent(EventBase @event, AuditUser user)
    {
        return new CommandContext
        {
            User = user,
            CorrelationId = @event.CorrelationId,
            CausationId = @event.EventId,
            TenantId = @event.TenantId,
            Language = @event.Language
        };
    }
}
