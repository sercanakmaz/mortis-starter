using Boilerplate.Tests.TestSupport.Infrastructure.Tenancy;
using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Infrastructure.Tenancy;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Infrastructure.Tenancy;

public class TenantCommandHandlerMiddlewareTests
{
    [Fact]
    public void Before_WithAContextCommand_Should_EnterTheTenantOfItsContext()
    {
        var tenantId = "acme";
        var tenantScope = TenantScopeBuilder.Create().Build();

        new TenantCommandHandlerMiddleware(tenantScope).Before(new FakeContextCommand { Context = new CommandContext { TenantId = tenantId } });

        Assert.Equal(tenantId, tenantScope.TenantId);
    }

    [Fact]
    public void Before_WithAnotherTenantThanTheScope_Should_ThrowForbidden()
    {
        var tenantScope = TenantScopeBuilder.Create().WithTenant("acme").Build();

        Assert.Throws<ForbiddenException>(() => new TenantCommandHandlerMiddleware(tenantScope).Before(new FakeContextCommand { Context = new CommandContext { TenantId = "globex" } }));
    }
}
