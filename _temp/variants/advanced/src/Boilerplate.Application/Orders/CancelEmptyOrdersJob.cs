using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Orders;

public class CancelEmptyOrdersJob(ICommandDispatcher commandDispatcher) : IRecurringJob
{
    public static string Name => "cancel-empty-orders";
    public static string DefaultCron => "0 * * * *";

    public async Task RunAsync(CommandContext commandContext, CancellationToken cancellationToken)
    {
        await commandDispatcher.Send(new CancelEmptyOrdersCommand { Context = commandContext });
    }
}
