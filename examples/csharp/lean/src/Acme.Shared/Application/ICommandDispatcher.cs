namespace Acme.Shared.Application;

public interface ICommandDispatcher
{
    Task<CommandResult<TContent>> Send<TContent>(ICommand<TContent> command);
}
