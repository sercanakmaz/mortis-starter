using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Console.Scenarios;

public static class ConsoleUserExtensions
{
    public static TCommand AsConsoleUser<TCommand>(this TCommand command)
    {
        if (command is IContextCommand contextCommand)
        {
            contextCommand.Context = new CommandContext
            {
                User = new AuditUser(ScenarioConstants.UserId, ScenarioConstants.UserEmail),
                CorrelationId = ScenarioConstants.RunCorrelationId
            };
        }

        return command;
    }
}
