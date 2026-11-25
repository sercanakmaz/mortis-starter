using Boilerplate.Shared.Application;
using Moq;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

public class CommandHandlerMiddlewareBuilder
{
    private readonly Mock<ICommandHandlerMiddleware> _mock = new();

    public static CommandHandlerMiddlewareBuilder Create() => new();

    public CommandHandlerMiddlewareBuilder WithRecordedCalls(List<string> calls)
    {
        _mock.Setup(commandHandlerMiddleware => commandHandlerMiddleware.Before(It.IsAny<ICommand>()))
            .Callback<ICommand>(_ => calls.Add(nameof(ICommandHandlerMiddleware.Before)));

        _mock.Setup(commandHandlerMiddleware => commandHandlerMiddleware.After(It.IsAny<ICommand>(), It.IsAny<CommandResult>()))
            .Callback<ICommand, CommandResult>((_, _) => calls.Add(nameof(ICommandHandlerMiddleware.After)));

        return this;
    }

    public ICommandHandlerMiddleware Build() => _mock.Object;
}
