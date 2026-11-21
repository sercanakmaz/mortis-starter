using Boilerplate.Tests.TestSupport.Infrastructure.Messaging;
using Boilerplate.Infrastructure.Tenancy;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Tenancy;

public class TenantScopeBuilder
{
    private readonly RecordingLogger<TenantScope> _logger = new();
    private string _tenantId;

    public static TenantScopeBuilder Create() => new();

    public TenantScopeBuilder WithTenant(Guid tenantId)
    {
        _tenantId = tenantId.ToString();
        return this;
    }

    public RecordingLogger<TenantScope> BuildLogger() => _logger;

    public TenantScope Build()
    {
        var tenantScope = new TenantScope(_logger);

        tenantScope.Enter(_tenantId);

        return tenantScope;
    }
}
