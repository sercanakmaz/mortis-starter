using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Infrastructure.Tenancy;

public class TenantScope(ILogger<TenantScope> logger)
{
    public string TenantId { get; private set; }
    public bool Bypassed { get; private set; }

    public void Enter(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId)) return;

        if (tenantId.Length > TenancyConstants.TenantIdMaxLength || tenantId.Any(char.IsWhiteSpace))
        {
            throw new ForbiddenException("A tenant that is not a valid id");
        }

        if (TenantId is not null && TenantId != tenantId)
        {
            throw new ForbiddenException("Switching tenant inside one scope");
        }

        TenantId = tenantId;
    }

    public string Required(string type)
    {
        if (Bypassed) return TenantId;

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
