using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Scheduling;

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
