namespace Boilerplate.Shared.Application;

public abstract class BaseCommandHandler<TCommand, TContent>(CommandPipeline<TCommand> commandPipeline) where TCommand : ICommand<TContent>
{
    protected abstract Task<CommandResult<TContent>> OnHandle(TCommand command);

    public Task<CommandResult<TContent>> Handle(TCommand command)
    {
        return commandPipeline.ExecuteAsync(command, OnHandle);
    }
}
