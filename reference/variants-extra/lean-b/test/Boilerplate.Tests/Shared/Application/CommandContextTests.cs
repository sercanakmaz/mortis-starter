using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Shared.Application;

namespace Boilerplate.Tests.Shared.Application;

public class CommandContextTests
{
    [Fact]
    public void Complete_WithoutAContext_Should_CreateOneWithACorrelationId()
    {
        var commandContext = CommandContext.Complete(null);

        Assert.NotEqual(Guid.Empty, commandContext.CorrelationId);
        Assert.Null(commandContext.User);
    }

    [Fact]
    public void Complete_WithACorrelationId_Should_KeepIt()
    {
        var correlationId = Guid.CreateVersion7();

        var commandContext = CommandContext.Complete(new CommandContext { CorrelationId = correlationId });

        Assert.Equal(correlationId, commandContext.CorrelationId);
    }

    [Fact]
    public void FromEvent_Should_KeepCorrelationTenantAndLanguageAndMakeTheEventTheCause()
    {
        var fakeEvent = new FakeEvent("first") { CorrelationId = Guid.CreateVersion7(), TenantId = "tenant-1", Language = "tr" };

        var commandContext = CommandContext.FromEvent(fakeEvent, FakeAuditUser.Create());

        Assert.Equal(fakeEvent.CorrelationId, commandContext.CorrelationId);
        Assert.Equal(fakeEvent.EventId, commandContext.CausationId);
        Assert.Equal(["tenant-1", "tr"], [commandContext.TenantId, commandContext.Language]);
        Assert.Equal(FakeAuditUser.Id, commandContext.User.Id);
    }
}
