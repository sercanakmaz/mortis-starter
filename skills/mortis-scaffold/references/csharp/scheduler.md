# Scheduler, C#

Applies when `addons` contains `scheduler` (R9: `hosts.worker` is not `none`) and `database` is `postgres` (with MongoDB not offered yet: R14, a note). Projects `{Name}.Application` (the jobs), `{Name}.Infrastructure` (folder `Scheduling/`) and the worker host. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

Package, in `{Name}.Infrastructure`: `Cronos`.

## How it fits together

```
IRecurringJob      Name, a cron schedule (settings, with a default), RunAsync(context) -> sends commands only
SchedulerService   in the worker (in-api: inside the API process), every PollSeconds:
  for each job: tick := the latest occurrence <= now (UTC) not older than the catch-up window
    claimed := insert into scheduled_job_runs (job, tick) on conflict do nothing   -- one replica wins a tick
    claimed -> run the job in its own scope, with the job's context; record finished_at or the error
one-shot job       scheduled_jobs (id, job, payload, run_at): claimed with for update skip locked, deleted when done
```

- A job holds no business rule: it sends commands, and the commands decide. It is the clock's equivalent of a controller.
- Several replicas are safe because the tick, not the replica, is the unit of work: the primary key `(job, tick)` lets one insert succeed.
- There is no system user (decision 47): each job runs as the user named in its settings (`Scheduler:Jobs:{Job}:UserId`), a service account the identity provider knows, with its own permissions. A job without a user does not start, and the worker logs why.
- A missed tick (every replica down) runs once when a worker comes back, if it is younger than `CatchUpMinutes` (10 by default); older ticks are skipped and logged. A job never runs twice for one tick, even if it failed; the next tick tries again.
- Cron expressions are five fields, evaluated in UTC.

## IRecurringJob

File `src/{Name}.Application/Scheduling/IRecurringJob.cs`:

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

`CancelEmptyOrdersCommand` loads the open orders whose lines are all cancelled through a new `IOrderRepository.FindOpenWithAllLinesCancelledTrackingAsync(int limit)` (100 at a time) and calls the existing `order.CancelWhenAllLinesCancelled()` on each; its handler test is in the style of the other order handlers.

## The run record

File `src/{Name}.Infrastructure/Scheduling/ScheduledJobRun.cs`: `Job` (text, 100), `Tick` (timestamp), `StartedAt`, `FinishedAt`, `Error` (text, 2000), primary key `(Job, Tick)`, table `scheduled_job_runs`; `ScheduledJobRunConfiguration.cs` maps it. `ScheduledJob.cs` and its configuration map `scheduled_jobs` for one-shot jobs: `Id`, `Job`, `Payload` (jsonb), `RunAt`, `Attempts`.

## SchedulerService

File `src/{Name}.Infrastructure/Scheduling/SchedulerService.cs`:

```csharp
public class SchedulerService(IServiceScopeFactory serviceScopeFactory, RecurringJobRegistry recurringJobRegistry, IOptions<SchedulerSettings> settings, ILogger<SchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
        var tick = CronExpression.Parse(jobSettings.Cron ?? recurringJob.DefaultCron).GetOccurrences(now.AddMinutes(-settings.Value.CatchUpMinutes), now, fromInclusive: true, toInclusive: true).LastOrDefault();

        if (tick == default || string.IsNullOrEmpty(jobSettings.UserId))
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

            logger.LogInformation("Job {Job} ran for tick {Tick}", recurringJob.Name, tick);
        }
        catch (Exception exception)
        {
            error = exception.Message;
```

The member continues:

```csharp
            logger.LogError(exception, "Job {Job} failed for tick {Tick}", recurringJob.Name, tick);
        }

        await appDbContext.Database.ExecuteSqlAsync($"update scheduled_job_runs set finished_at = {DateTime.UtcNow}, error = {error} where job = {recurringJob.Name} and tick = {tick}", CancellationToken.None);
    }
}
```

`RecurringJobRegistry` holds `RecurringJobEntry(string Name, string DefaultCron, Type JobType)` records; `SchedulerModule.AddRecurringJob<TJob>()` adds one (`new RecurringJobEntry(TJob.Name, TJob.DefaultCron, typeof(TJob))`) and registers `TJob` scoped. The worker host calls `services.AddScheduler(configuration).AddRecurringJob<CancelEmptyOrdersJob>();`, which also adds `SchedulerService` and the one-shot `ScheduledJobService` (the same loop over `scheduled_jobs`, claimed with `for update skip locked` as the outbox does). With tenancy, a job that must run for every tenant sends one command per tenant id it reads from its own settings list; the scheduler never guesses tenants.

`SchedulerSettings`: `PollSeconds` (15), `CatchUpMinutes` (10), `Jobs` (a dictionary by job name of `SchedulerJobSettings`: `Cron`, `UserId`, `UserEmail`, `TenantId`).

## Tests

`SchedulerServiceTests` cannot fake the clock cheaply, so the cron arithmetic is a pure method tested alone: `SchedulerTicks.Due(cron, now, catchUpMinutes)` returns the expected tick for "every hour at minute 0" at 10:00:30, none at 10:30 for a tick older than the window, and the latest of several missed ticks. `CancelEmptyOrdersCommandHandlerTests` as for the other order handlers. At `testing` = `integration` and above, `SchedulerClaimTests` in the integration project: two concurrent claims of one tick give one run row and one success. `scripts/conformance/scheduler.sh` runs two workers.

## Files this add-on changes

The worker's `Program.cs` and `appsettings.json` (`"Scheduler": { "Jobs": { "cancel-empty-orders": { "UserId": "", "UserEmail": "" } } }`), `IOrderRepository.cs`, `OrderRepository.cs`, `ApplicationModule.cs` (the command), `AppDbContext.cs` (the two configurations); a migration adds the two tables.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `IRecurringJob.cs` | `{Name}.Application.Scheduling` | `{Name}.Shared.Application` |
| `CancelEmptyOrdersJob.cs`, `CancelEmptyOrdersCommand.cs` | `{Name}.Application.Orders` | `{Name}.Application.Scheduling`, `{Name}.Domain.Aggregates.Orders`, `{Name}.Shared.Application` |
| `SchedulerService.cs`, `ScheduledJobService.cs`, `SchedulerModule.cs`, `SchedulerSettings.cs`, `SchedulerTicks.cs`, `RecurringJobRegistry.cs`, `ScheduledJobRun.cs`, `ScheduledJob.cs` and the two configurations | `{Name}.Infrastructure.Scheduling` | `{Name}.Application.Scheduling`, `{Name}.Infrastructure.Persistence`, `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `Cronos`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options` |

## Decision record

`docs/adr/0026-scheduled-jobs.md`, in `project-docs-adr.md`.
