using Boilerplate.Tests.TestSupport.Infrastructure.Tenancy;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Persistence;

public class AppDbContextBuilder
{
    private TenantScope _tenantScope = TenantScopeBuilder.Create().Build();

    public static AppDbContextBuilder Create() => new();

    public AppDbContextBuilder WithTenantScope(TenantScope tenantScope)
    {
        _tenantScope = tenantScope;
        return this;
    }

    public AppDbContext Build()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=never-connected;Database=unit-tests")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, _tenantScope);
    }
}
