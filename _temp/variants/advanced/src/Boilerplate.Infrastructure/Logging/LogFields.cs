using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Logging;

public static class LogFields
{
    public static Dictionary<string, object> Of(ICommand command)
    {
        var commandContext = (command as IContextCommand)?.Context;

        return new Dictionary<string, object>
        {
            [LoggingConstants.CorrelationId] = commandContext?.CorrelationId,
            ["CausationId"] = commandContext?.CausationId,
            ["TenantId"] = commandContext?.TenantId,
            ["UserId"] = commandContext?.User?.Id,
            ["Language"] = commandContext?.Language,
            ["Command"] = command.GetType().Name,
            ["Chain"] = string.Join(LoopGuardConstants.Separator, commandContext?.Chain ?? [])
        };
    }

    public static Dictionary<string, object> Of(EventBase @event)
    {
        return new Dictionary<string, object>
        {
            [LoggingConstants.CorrelationId] = @event.CorrelationId,
            ["CausationId"] = @event.CausationId,
            ["TenantId"] = @event.TenantId,
            ["UserId"] = @event.UserId,
            ["Language"] = @event.Language,
            ["Event"] = @event.GetType().Name,
            ["EventId"] = @event.EventId,
            ["Chain"] = string.Join(LoopGuardConstants.Separator, @event.Chain ?? [])
        };
    }
}
