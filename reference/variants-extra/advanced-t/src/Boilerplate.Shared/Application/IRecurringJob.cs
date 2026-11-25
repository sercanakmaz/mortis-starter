namespace Boilerplate.Shared.Application;

public interface IRecurringJob
{
    static abstract string Name { get; }
    static abstract string DefaultCron { get; }

    Task RunAsync(CommandContext commandContext, CancellationToken cancellationToken);
}
