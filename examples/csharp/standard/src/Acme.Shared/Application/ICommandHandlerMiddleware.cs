namespace Acme.Shared.Application;

public interface ICommandHandlerMiddleware
{
    void Before(ICommand command);

    void After(ICommand command, CommandResult commandResult);
}
