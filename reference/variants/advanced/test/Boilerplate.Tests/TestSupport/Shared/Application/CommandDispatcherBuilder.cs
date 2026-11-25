using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Shared.Application;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

public class CommandDispatcherBuilder
{
    private readonly ServiceCollection _services = new();

    public static CommandDispatcherBuilder Create() => new();

    public CommandDispatcherBuilder WithCommandHandler<TCommand, TContent>(BaseCommandHandler<TCommand, TContent> commandHandler) where TCommand : ICommand<TContent>
    {
        _services.AddSingleton(commandHandler);
        return this;
    }

    public ICommandDispatcher Build()
    {
        return new CommandDispatcher(_services.BuildServiceProvider());
    }
}
