using Boilerplate.Shared.Application;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Infrastructure.Logging;

public class LoggingCommandHandlerMiddleware(ILogger<LoggingCommandHandlerMiddleware> logger) : ICommandHandlerMiddleware
{
    private readonly Stack<IDisposable> _scopes = new();

    public void Before(ICommand command)
    {
        _scopes.Push(logger.BeginScope(LogFields.Of(command)));

        logger.LogInformation("{Command} started", command.GetType().Name);
    }

    public void After(ICommand command, CommandResult commandResult)
    {
        logger.LogInformation("{Command} finished", command.GetType().Name);

        if (_scopes.TryPop(out var scope))
        {
            scope.Dispose();
        }
    }
}
