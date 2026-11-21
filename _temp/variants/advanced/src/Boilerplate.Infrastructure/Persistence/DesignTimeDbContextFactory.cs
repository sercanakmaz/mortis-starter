using Microsoft.EntityFrameworkCore.Design;
using Boilerplate.Infrastructure.Tenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(PersistenceConstants.ConnectionStringVariable) ?? PersistenceConstants.DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new TenantScope(NullLogger<TenantScope>.Instance));
    }
}
