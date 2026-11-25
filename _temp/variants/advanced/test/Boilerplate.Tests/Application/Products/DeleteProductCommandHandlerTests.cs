using Boilerplate.Application.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Tests.TestSupport.Shared.Application;

namespace Boilerplate.Tests.Application.Products;

public class DeleteProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_MarkTheProductDeletedByTheCaller()
    {
        var product = FakeProduct.Create("SKU-1", 10);
        var commandHandler = new DeleteProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(product).WithUpdateAsync().Build(), CommandPipelineBuilder<DeleteProductCommand>.Create().Build());

        await commandHandler.Handle(new DeleteProductCommand(product.Id) { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal((true, "user-1"), (product.IsDeleted, product.DeletedUserId));
    }

    [Fact]
    public async Task Handle_WithAnUnknownOrDeletedProduct_Should_ThrowNotFound()
    {
        var commandHandler = new DeleteProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).Build(), CommandPipelineBuilder<DeleteProductCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new DeleteProductCommand(Guid.CreateVersion7())));
    }
}
