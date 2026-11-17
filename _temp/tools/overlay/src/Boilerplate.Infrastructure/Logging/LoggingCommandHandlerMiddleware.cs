using Boilerplate.Shared.Application;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Infrastructure.Logging;

public class LoggingCommandHandlerMiddleware(ILogger<LoggingCommandHandlerMiddleware> logger) : ICommandHandlerMiddleware
{
    public void Before(ICommand command)
    {
        var commandContext = (command as IContextCommand)?.Context;

        logger.LogInformation("{Command} started correlation {CorrelationId} causation {CausationId}", command.GetType().Name, commandContext?.CorrelationId, commandContext?.CausationId);
    }

    public void After(ICommand command, CommandResult commandResult)
    {
        var commandContext = (command as IContextCommand)?.Context;

        logger.LogInformation("{Command} finished correlation {CorrelationId}", command.GetType().Name, commandContext?.CorrelationId);
    }
}
