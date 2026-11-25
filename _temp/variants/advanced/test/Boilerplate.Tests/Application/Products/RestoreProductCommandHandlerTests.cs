using Boilerplate.Application.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Tests.TestSupport.Shared.Application;

namespace Boilerplate.Tests.Application.Products;

public class RestoreProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_FindTheProductAmongTheDeletedAndRestoreIt()
    {
        var product = FakeProduct.Create("SKU-1", 10);
        product.Delete(new AuditUser("user-1", "user-1@localhost"));
        var commandHandler = new RestoreProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdIncludingDeletedTrackingAsync(product).WithUpdateAsync().Build(), CommandPipelineBuilder<RestoreProductCommand>.Create().Build());

        var commandResult = await commandHandler.Handle(new RestoreProductCommand(product.Id) { Context = new CommandContext { User = new AuditUser("user-2", "user-2@localhost") } });

        Assert.False(product.IsDeleted);
        Assert.Equal(("SKU-1", "user-2"), (commandResult.Content.Sku, product.UpdatedUserId));
    }

    [Fact]
    public async Task Handle_WithAnUnknownProduct_Should_ThrowNotFound()
    {
        var commandHandler = new RestoreProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdIncludingDeletedTrackingAsync(null).Build(), CommandPipelineBuilder<RestoreProductCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new RestoreProductCommand(Guid.CreateVersion7())));
    }
}
