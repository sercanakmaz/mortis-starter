using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Tests.TestSupport.Scenarios;
using Moq;

namespace Boilerplate.Tests.Scenarios;

public class CreateProductScenarioTests
{
    [Fact]
    public async Task Send_WithAnInvalidCommand_Should_FailValidationBeforeAnyWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithCreateAsync();
        var commandDispatcher = ScenarioHost.Create().With(productRepositoryBuilder.Build()).Dispatcher();

        await Assert.ThrowsAsync<ValidationFailedException>(() => commandDispatcher.Send(new CreateProductCommand { Sku = "", Stock = -1 }));

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.CreateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Send_Should_RunTheRegisteredHandlerAndWriteOnce()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithCreateAsync();
        var commandDispatcher = ScenarioHost.Create().With(productRepositoryBuilder.Build()).Dispatcher();

        var commandResult = await commandDispatcher.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1, Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal("SKU-1", commandResult.Content.Sku);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.CreateAsync(It.IsAny<Product>()), Times.Once);
    }
}
