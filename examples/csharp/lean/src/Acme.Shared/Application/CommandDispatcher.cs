using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Shared.Application;

public sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> CommandHandlerInvokers = new();

    public Task<CommandResult<TContent>> Send<TContent>(ICommand<TContent> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var commandHandlerInvoker = (CommandHandlerInvoker<TContent>)CommandHandlerInvokers.GetOrAdd(
            command.GetType(),
            commandType => Activator.CreateInstance(typeof(CommandHandlerInvoker<,>).MakeGenericType(commandType, typeof(TContent))));

        return commandHandlerInvoker.Invoke(serviceProvider, command);
    }
}

public abstract class CommandHandlerInvoker<TContent>
{
    public abstract Task<CommandResult<TContent>> Invoke(IServiceProvider serviceProvider, ICommand<TContent> command);
}

public sealed class CommandHandlerInvoker<TCommand, TContent> : CommandHandlerInvoker<TContent> where TCommand : ICommand<TContent>
{
    public override Task<CommandResult<TContent>> Invoke(IServiceProvider serviceProvider, ICommand<TContent> command)
    {
        return serviceProvider.GetRequiredService<BaseCommandHandler<TCommand, TContent>>().Handle((TCommand)command);
    }
}
