using Boilerplate.Shared.Application;

namespace Boilerplate.Infrastructure.Tenancy;

public class TenantCommandHandlerMiddleware(TenantScope tenantScope) : ICommandHandlerMiddleware
{
    public void Before(ICommand command)
    {
        if (command is IContextCommand contextCommand)
        {
            tenantScope.Enter(contextCommand.Context?.TenantId);
        }
    }

    public void After(ICommand command, CommandResult commandResult)
    {
    }
}
