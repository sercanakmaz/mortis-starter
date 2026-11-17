using Acme.Shared.Application;

namespace Acme.Tests.TestSupport.Shared.Application;

public class FakeUserCommandHandler(CommandPipeline<FakeUserCommand> commandPipeline, List<FakeUserCommand> handled) : BaseCommandHandler<FakeUserCommand, string>(commandPipeline)
{
    protected override Task<CommandResult<string>> OnHandle(FakeUserCommand command)
    {
        handled.Add(command);

        return Task.FromResult(CommandResult.FromContent(command.Name));
    }
}
