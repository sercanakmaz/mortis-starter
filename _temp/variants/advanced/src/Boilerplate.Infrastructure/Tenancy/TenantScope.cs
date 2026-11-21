using Boilerplate.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Infrastructure.Tenancy;

public class TenantScope(ILogger<TenantScope> logger)
{
    public Guid? TenantId { get; private set; }
    public bool Bypassed { get; private set; }

    public void Enter(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId)) return;

        if (!Guid.TryParse(tenantId, out var parsed))
        {
            throw new ForbiddenException("A tenant that is not a valid id");
        }

        if (TenantId is not null && TenantId != parsed)
        {
            throw new ForbiddenException("Switching tenant inside one scope");
        }

        TenantId = parsed;
    }

    public Guid Required(string type)
    {
        if (Bypassed) return TenantId ?? Guid.Empty;

        return TenantId ?? throw new TenantRequiredException(type);
    }

    public IDisposable Bypass(string reason)
    {
        logger.LogWarning("The tenant filter is bypassed: {Reason}", reason);

        Bypassed = true;

        return new BypassRelease(this);
    }

    private sealed class BypassRelease(TenantScope tenantScope) : IDisposable
    {
        public void Dispose()
        {
            tenantScope.Bypassed = false;
        }
    }
}
