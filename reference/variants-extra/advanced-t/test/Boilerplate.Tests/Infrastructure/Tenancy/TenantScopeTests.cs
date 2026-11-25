using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Infrastructure.Tenancy;
using Boilerplate.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Tests.Infrastructure.Tenancy;

public class TenantScopeTests
{
    [Fact]
    public void Enter_WithAnId_Should_MakeItTheTenantOfTheScope()
    {
        var tenantId = "acme";

        var tenantScope = TenantScopeBuilder.Create().WithTenant(tenantId).Build();

        Assert.Equal(tenantId, tenantScope.Required("Product"));
    }

    [Fact]
    public void Required_WithoutATenant_Should_ThrowTenantRequired()
    {
        var tenantScope = TenantScopeBuilder.Create().Build();

        tenantScope.Enter(null);

        Assert.Equal("tenant_required", Assert.Throws<TenantRequiredException>(() => tenantScope.Required("Product")).Code);
    }

    [Fact]
    public void Enter_WithAValueThatIsNotAnId_Should_ThrowForbidden()
    {
        var tenantScope = TenantScopeBuilder.Create().Build();

        Assert.Throws<ForbiddenException>(() => tenantScope.Enter("acme corp"));
        Assert.Throws<ForbiddenException>(() => tenantScope.Enter(new string('a', TenancyConstants.TenantIdMaxLength + 1)));
    }

    [Fact]
    public void Enter_WithAnotherTenant_Should_ThrowForbidden_And_WithTheSameTenant_Should_Pass()
    {
        var tenantScope = TenantScopeBuilder.Create().WithTenant("acme").Build();

        tenantScope.Enter("acme");

        Assert.Throws<ForbiddenException>(() => tenantScope.Enter("globex"));
    }

    [Fact]
    public void Bypass_Should_LetRequiredPassWithoutATenantLogAWarningAndEndWhenDisposed()
    {
        var tenantScopeBuilder = TenantScopeBuilder.Create();
        var tenantScope = tenantScopeBuilder.Build();

        using (tenantScope.Bypass("nightly cleanup"))
        {
            Assert.Null(tenantScope.Required("Product"));
        }

        Assert.Throws<TenantRequiredException>(() => tenantScope.Required("Product"));
        Assert.Single(tenantScopeBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Warning);
    }
}
