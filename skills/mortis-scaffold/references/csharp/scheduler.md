# Scheduler, C#

Applies when `addons` contains `scheduler` (R9: `hosts.worker` is not `none`) and `database` is `postgres` (with MongoDB not offered yet: R14, a note). Projects `{Name}.Shared` (`IRecurringJob`), `{Name}.Application` (the jobs), `{Name}.Infrastructure` (folder `Scheduling/`) and the worker host. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Built** in the advanced reference solution (decision 52): compiled, tested and verified there, two Workers at once.

Package, in `{Name}.Infrastructure`: `Cronos` 0.13.0.

## How it fits together

```
IRecurringJob      Name, a cron schedule (settings, with a default), RunAsync(context) -> sends commands only
SchedulerService   in the worker (in-api: inside the API process), every PollSeconds:
  for each job: tick := the latest occurrence <= now (UTC) not older than the catch-up window
    claimed := insert into scheduled_job_runs (job, tick) on conflict do nothing   -- one replica wins a tick
    claimed -> run the job in its own scope, with the job's context; record finished_at or the error
```

- A job holds no business rule: it sends commands, and the commands decide. It is the clock's equivalent of a controller.
- Several replicas are safe because the tick, not the replica, is the unit of work: the primary key `(job, tick)` lets one insert succeed.
- There is no system user (decision 47): each job runs as the user named in its settings (`Scheduler:Jobs:{Job}:UserId`), a service account the identity provider knows, with its own permissions. A job without a user does not start, and the worker logs why.
- A missed tick (every replica down) runs once when a worker comes back, if it is younger than `CatchUpMinutes` (10 by default); older ticks are skipped and logged. A job never runs twice for one tick, even if it failed; the next tick tries again.
- Cron expressions are five fields, evaluated in UTC.
- One-shot jobs (run this once at a time) are not generated: a project that needs them adds a table claimed with `for update skip locked`, as the outbox does.

## IRecurringJob

File `src/{Name}.Shared/Application/IRecurringJob.cs` (in Shared, because Infrastructure, which runs the jobs, does not reference Application):

```csharp
public interface IRecurringJob
{
    static abstract string Name { get; }
    static abstract string DefaultCron { get; }

    Task RunAsync(CommandContext commandContext, CancellationToken cancellationToken);
}
```

The example's job, file `src/{Name}.Application/Orders/CancelEmptyOrdersJob.cs`, sweeps orders whose lines were all cancelled but whose reaction did not run (a sync handler normally cancels them at once):

```csharp
public class CancelEmptyOrdersJob(ICommandDispatcher commandDispatcher) : IRecurringJob
{
    public static string Name => "cancel-empty-orders";
    public static string DefaultCron => "0 * * * *";

    public async Task RunAsync(CommandContext commandContext, CancellationToken cancellationToken)
    {
        await commandDispatcher.Send(new CancelEmptyOrdersCommand { Context = commandContext });
    }
}
```

File `src/{Name}.Application/Orders/CancelEmptyOrdersCommand.cs` (registered with `services.AddCommandHandler<CancelEmptyOrdersCommand, int, CancelEmptyOrdersCommandHandler>();`; `OrderConstants` gains `public const int SweepBatchSize = 100;`):

```csharp
public record CancelEmptyOrdersCommand : ICommand<int>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class CancelEmptyOrdersCommandHandler(
    IOrderRepository orderRepository,
    CommandPipeline<CancelEmptyOrdersCommand> commandPipeline) : BaseCommandHandler<CancelEmptyOrdersCommand, int>(commandPipeline)
{
    protected override async Task<CommandResult<int>> OnHandle(CancelEmptyOrdersCommand command)
    {
        var orders = await orderRepository.FindOpenWithAllLinesCancelledTrackingAsync(OrderConstants.SweepBatchSize);

        foreach (var order in orders.Where(order => order.CancelWhenAllLinesCancelled()))
        {
            order.SetUpdatedAudit(command.Context.User);

            await orderRepository.UpdateAsync(order);
        }

        return CommandResult.FromContent(orders.Count);
    }
}
```

`IOrderRepository` gains `Task<IReadOnlyList<Order>> FindOpenWithAllLinesCancelledTrackingAsync(int limit);`, in `OrderRepository` (using `Microsoft.EntityFrameworkCore`):

```csharp
    public async Task<IReadOnlyList<Order>> FindOpenWithAllLinesCancelledTrackingAsync(int limit)
    {
        return await DbSet
            .Where(order => order.Status == OrderStatus.Open && order.Lines.Any() && order.Lines.All(line => line.IsCancelled))
            .OrderBy(order => order.Id)
            .Take(limit)
            .ToListAsync();
    }
```

## The run record

File `src/{Name}.Infrastructure/Scheduling/ScheduledJobRun.cs`:

```csharp
public class ScheduledJobRun
{
    public string Job { get; private set; }
    public DateTime Tick { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string Error { get; private set; }
}
```

File `src/{Name}.Infrastructure/Scheduling/ScheduledJobRunConfiguration.cs`:

```csharp
public class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
{
    public void Configure(EntityTypeBuilder<ScheduledJobRun> builder)
    {
        builder.ToTable(SchedulerSettings.RunsTable);
        builder.HasKey(run => new { run.Job, run.Tick });
        builder.Property(run => run.Job).HasMaxLength(SchedulerSettings.JobMaxLength);
        builder.Property(run => run.Error).HasMaxLength(SchedulerSettings.ErrorMaxLength);
    }
}
```

File `src/{Name}.Infrastructure/Scheduling/SchedulerSettings.cs`:

```csharp
public class SchedulerSettings
{
    public const string Section = "Scheduler";
    public const string RunsTable = "scheduled_job_runs";
    public const int JobMaxLength = 100;
    public const int ErrorMaxLength = 2000;

    public int PollSeconds { get; set; } = 15;
    public int CatchUpMinutes { get; set; } = 10;
    public Dictionary<string, SchedulerJobSettings> Jobs { get; set; } = [];
}

public class SchedulerJobSettings
{
    public string Cron { get; set; }
    public string UserId { get; set; }
    public string UserEmail { get; set; }
    public string TenantId { get; set; }
}
```

`ApplyConfigurationsFromAssembly` picks the configuration up; regenerate the `Initial` migration.

## SchedulerService

File `src/{Name}.Infrastructure/Scheduling/SchedulerTicks.cs`:

```csharp
public static class SchedulerTicks
{
    public static DateTime? Due(string cron, DateTime now, int catchUpMinutes)
    {
        var tick = CronExpression.Parse(cron).GetOccurrences(now.AddMinutes(-catchUpMinutes), now, fromInclusive: true, toInclusive: true).LastOrDefault();

        return tick == default ? null : tick;
    }
}
```

File `src/{Name}.Infrastructure/Scheduling/RecurringJobRegistry.cs`:

```csharp
public record RecurringJobEntry(string Name, string DefaultCron, Type JobType);

public class RecurringJobRegistry(IEnumerable<RecurringJobEntry> recurringJobEntries)
{
    public IReadOnlyList<RecurringJobEntry> Jobs { get; } = recurringJobEntries.ToList();
}
```

File `src/{Name}.Infrastructure/Scheduling/SchedulerService.cs`:

```csharp
public class SchedulerService(IServiceScopeFactory serviceScopeFactory, RecurringJobRegistry recurringJobRegistry, IOptions<SchedulerSettings> settings, ILogger<SchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var recurringJob in recurringJobRegistry.Jobs.Where(recurringJob => string.IsNullOrEmpty(settings.Value.Jobs.GetValueOrDefault(recurringJob.Name)?.UserId)))
        {
            logger.LogWarning("Job {Job} does not run: Scheduler:Jobs:{Job}:UserId names no service account", recurringJob.Name, recurringJob.Name);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var recurringJob in recurringJobRegistry.Jobs)
            {
                try
                {
                    await RunDueAsync(recurringJob, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Job {Job} could not be scheduled; it tries again at the next poll", recurringJob.Name);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.Value.PollSeconds), stoppingToken);
        }
    }

    private async Task RunDueAsync(RecurringJobEntry recurringJob, CancellationToken stoppingToken)
    {
        var jobSettings = settings.Value.Jobs.GetValueOrDefault(recurringJob.Name) ?? new SchedulerJobSettings();
        var now = DateTime.UtcNow;

        if (SchedulerTicks.Due(jobSettings.Cron ?? recurringJob.DefaultCron, now, settings.Value.CatchUpMinutes) is not { } tick || string.IsNullOrEmpty(jobSettings.UserId))
        {
            return;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var claimed = await appDbContext.Database.ExecuteSqlAsync($"insert into scheduled_job_runs (job, tick, started_at) values ({recurringJob.Name}, {tick}, {now}) on conflict do nothing", stoppingToken);

        if (claimed == 0)
        {
            return;
        }

        var commandContext = new CommandContext { User = new AuditUser(jobSettings.UserId, jobSettings.UserEmail), TenantId = jobSettings.TenantId, CorrelationId = Guid.CreateVersion7() };
        string error = null;

        try
        {
            await ((IRecurringJob)scope.ServiceProvider.GetRequiredService(recurringJob.JobType)).RunAsync(commandContext, stoppingToken);
```

The member continues:

```csharp
            logger.LogInformation("Job {Job} ran for tick {Tick}", recurringJob.Name, tick);
        }
        catch (Exception exception)
        {
            error = exception.Message[..Math.Min(exception.Message.Length, SchedulerSettings.ErrorMaxLength)];

            logger.LogError(exception, "Job {Job} failed for tick {Tick}", recurringJob.Name, tick);
        }

        await appDbContext.Database.ExecuteSqlAsync($"update scheduled_job_runs set finished_at = {DateTime.UtcNow}, error = {error} where job = {recurringJob.Name} and tick = {tick}", CancellationToken.None);
    }
}
```

File `src/{Name}.Infrastructure/Scheduling/SchedulerModule.cs`:

```csharp
public static class SchedulerModule
{
    public static IServiceCollection AddScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SchedulerSettings>(configuration.GetSection(SchedulerSettings.Section));
        services.AddSingleton<RecurringJobRegistry>();
        services.AddHostedService<SchedulerService>();

        return services;
    }

    public static IServiceCollection AddRecurringJob<TJob>(this IServiceCollection services) where TJob : class, IRecurringJob
    {
        services.AddScoped<TJob>();
        services.AddSingleton(new RecurringJobEntry(TJob.Name, TJob.DefaultCron, typeof(TJob)));

        return services;
    }
}
```

The worker host calls `.AddScheduler(builder.Configuration).AddRecurringJob<CancelEmptyOrdersJob>()` (usings `{Name}.Application.Orders`, `{Name}.Infrastructure.Scheduling`). With tenancy each job runs for the one tenant in its settings (`TenantId`); a job that must run for every tenant sends one command per tenant id it reads from its own settings list; the scheduler never guesses tenants.

## Tests

`test/{Name}.Tests/Infrastructure/Scheduling/SchedulerTicksTests.cs`, the cron arithmetic alone:

```csharp
public class SchedulerTicksTests
{
    [Theory]
    [InlineData("0 * * * *", "2026-01-01T10:00:30Z", 10, "2026-01-01T10:00:00Z")]
    [InlineData("0 * * * *", "2026-01-01T10:30:00Z", 10, null)]
    [InlineData("*/5 * * * *", "2026-01-01T10:17:00Z", 10, "2026-01-01T10:15:00Z")]
    public void Due_Should_ReturnTheLatestTickInsideTheCatchUpWindow(string cron, string now, int catchUpMinutes, string expected)
    {
        var tick = SchedulerTicks.Due(cron, DateTime.Parse(now).ToUniversalTime(), catchUpMinutes);

        Assert.Equal(expected is null ? null : DateTime.Parse(expected).ToUniversalTime(), tick);
    }
}
```

`OrderRepositoryBuilder` gains `WithFindOpenWithAllLinesCancelledTrackingAsync(params Order[] orders)`. `test/{Name}.Tests/Application/Orders/CancelEmptyOrdersCommandHandlerTests.cs`:

```csharp
public class CancelEmptyOrdersCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CancelEveryOrderWhoseLinesAreAllCancelledAsTheJobsUser()
    {
        var written = new List<Order>();
        var emptyOrder = FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1").WithoutEvents();
        var activeOrder = FakeOrder.Create().WithLine("SKU-2").WithoutEvents();
        var orderRepository = OrderRepositoryBuilder.Create().WithFindOpenWithAllLinesCancelledTrackingAsync(emptyOrder, activeOrder).WithUpdateAsync(written.Add).Build();
        var commandHandler = new CancelEmptyOrdersCommandHandler(orderRepository, CommandPipelineBuilder<CancelEmptyOrdersCommand>.Create().Build());

        var commandResult = await commandHandler.Handle(new CancelEmptyOrdersCommand { Context = new CommandContext { User = new AuditUser("scheduler", "scheduler@localhost") } });

        Assert.Equal(2, commandResult.Content);
        Assert.Equal((OrderStatus.Cancelled, "scheduler"), (Assert.Single(written).Status, emptyOrder.UpdatedUserId));
        Assert.Equal(OrderStatus.Open, activeOrder.Status);
    }
}
```

`scripts/conformance/scheduler.sh` runs two Workers for two minutes and proves one run per tick: the claim across replicas is proved there, not by a test.

## Files this add-on changes

The worker's `Program.cs` and `appsettings.json` (`"Scheduler": { "Jobs": { "cancel-empty-orders": { "UserId": "", "UserEmail": "", "TenantId": "" } } }`), `IOrderRepository.cs`, `OrderRepository.cs`, `OrderConstants.cs`, `ApplicationModule.cs` (the command); the regenerated migration adds `scheduled_job_runs`.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Application/Orders/CancelEmptyOrdersCommand.cs` | `{Name}.Application.Orders` | System.Text.Json.Serialization, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Application/Orders/CancelEmptyOrdersJob.cs` | `{Name}.Application.Orders` | {Name}.Shared.Application |
| `src/{Name}.Infrastructure/Scheduling/RecurringJobRegistry.cs` | `{Name}.Infrastructure.Scheduling` | none |
| `src/{Name}.Infrastructure/Scheduling/ScheduledJobRun.cs` | `{Name}.Infrastructure.Scheduling` | none |
| `src/{Name}.Infrastructure/Scheduling/ScheduledJobRunConfiguration.cs` | `{Name}.Infrastructure.Scheduling` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders |
| `src/{Name}.Infrastructure/Scheduling/SchedulerModule.cs` | `{Name}.Infrastructure.Scheduling` | Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection, {Name}.Shared.Application |
| `src/{Name}.Infrastructure/Scheduling/SchedulerService.cs` | `{Name}.Infrastructure.Scheduling` | Microsoft.EntityFrameworkCore, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging, Microsoft.Extensions.Options, {Name}.Infrastructure.Persistence, {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Scheduling/SchedulerSettings.cs` | `{Name}.Infrastructure.Scheduling` | none |
| `src/{Name}.Infrastructure/Scheduling/SchedulerTicks.cs` | `{Name}.Infrastructure.Scheduling` | Cronos |
| `src/{Name}.Shared/Application/IRecurringJob.cs` | `{Name}.Shared.Application` | none |
| `test/{Name}.Tests/Application/Orders/CancelEmptyOrdersCommandHandlerTests.cs` | `{Name}.Tests.Application.Orders` | {Name}.Application.Orders, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Domain.Orders, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Infrastructure/Scheduling/SchedulerTicksTests.cs` | `{Name}.Tests.Infrastructure.Scheduling` | {Name}.Infrastructure.Scheduling |

## Decision record

`docs/adr/0026-scheduled-jobs.md`, in `project-docs-adr.md`.
