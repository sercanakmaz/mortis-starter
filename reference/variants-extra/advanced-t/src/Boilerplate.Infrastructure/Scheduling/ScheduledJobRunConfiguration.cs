using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Scheduling;

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
