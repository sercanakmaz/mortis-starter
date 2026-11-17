using Acme.Shared.Application;
using Microsoft.Extensions.Logging;

namespace Acme.Infrastructure.Logging;

public class LoggingCommandHandlerMiddleware(ILogger<LoggingCommandHandlerMiddleware> logger) : ICommandHandlerMiddleware
{
    public void Before(ICommand command)
    {
        logger.LogInformation("{Command} started", command.GetType().Name);
    }

    public void After(ICommand command, CommandResult commandResult)
    {
        logger.LogInformation("{Command} finished", command.GetType().Name);
    }
}
