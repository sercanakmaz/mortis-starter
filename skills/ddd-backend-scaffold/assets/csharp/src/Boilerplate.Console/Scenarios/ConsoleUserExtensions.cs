using Boilerplate.Shared.Application;

namespace Boilerplate.Console.Scenarios;

public static class ConsoleUserExtensions
{
    public static TCommand AsConsoleUser<TCommand>(this TCommand command)
    {
        if (command is IUserCommand userCommand)
        {
            userCommand.UserId = ScenarioConstants.UserId;
            userCommand.UserEmail = ScenarioConstants.UserEmail;
        }

        return command;
    }
}
