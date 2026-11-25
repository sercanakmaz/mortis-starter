using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Application.Products;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.Application.Products;

public class RenameProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_StoreTheNameUnderTheCallersLanguageAndReturnItInThatLanguage()
    {
        var product = FakeProduct.Create("SKU-1", 10);
        var commandPipeline = CommandPipelineBuilder<RenameProductCommand>.Create().WithValidator(new RenameProductCommandValidator()).Build();
        var commandHandler = new RenameProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(product).WithUpdateAsync().Build(), commandPipeline);

        var commandResult = await commandHandler.Handle(new RenameProductCommand { Id = product.Id, Name = "Cıvata", Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost"), Language = "tr" } });

        Assert.Equal("Cıvata", commandResult.Content.Name);
        Assert.Equal(["Default Product", "Cıvata"], [commandResult.Content.Names[Languages.Default], commandResult.Content.Names["tr"]]);
        Assert.Equal("user-1", product.UpdatedUserId);
    }

    [Fact]
    public async Task Handle_WithAnUnknownProduct_Should_ThrowNotFound()
    {
        var commandPipeline = CommandPipelineBuilder<RenameProductCommand>.Create().WithValidator(new RenameProductCommandValidator()).Build();
        var commandHandler = new RenameProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).Build(), commandPipeline);

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new RenameProductCommand { Id = Guid.CreateVersion7(), Name = "Bolt" }));
    }
}
