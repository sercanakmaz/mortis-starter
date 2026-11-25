namespace Boilerplate.Infrastructure.Scheduling;

public record RecurringJobEntry(string Name, string DefaultCron, Type JobType);

public class RecurringJobRegistry(IEnumerable<RecurringJobEntry> recurringJobEntries)
{
    public IReadOnlyList<RecurringJobEntry> Jobs { get; } = recurringJobEntries.ToList();
}
