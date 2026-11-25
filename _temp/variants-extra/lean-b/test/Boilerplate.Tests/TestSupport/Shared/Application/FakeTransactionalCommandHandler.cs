using Boilerplate.Shared.Application;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

public class FakeTransactionalCommandHandler(CommandPipeline<FakeTransactionalCommand> commandPipeline, List<FakeTransactionalCommand> handled) : BaseCommandHandler<FakeTransactionalCommand, string>(commandPipeline)
{
    protected override Task<CommandResult<string>> OnHandle(FakeTransactionalCommand command)
    {
        handled.Add(command);

        return Task.FromResult(CommandResult.FromContent(command.Name));
    }
}
