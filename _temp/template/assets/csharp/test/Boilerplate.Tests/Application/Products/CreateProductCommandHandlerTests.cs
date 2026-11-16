using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Application.Products;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;
using Moq;

namespace Boilerplate.Tests.Application.Products;

public class CreateProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnTheCreatedProductInsideATransaction()
    {
        var calls = new List<string>();
        var productService = ProductServiceBuilder.Create()
            .WithProductRepository(ProductRepositoryBuilder.Create().WithIsSkuExistsAsync(false).WithCreateAsync(_ => calls.Add(nameof(IProductRepository.CreateAsync))).Build())
            .Build();
        var commandPipeline = CommandPipelineBuilder<CreateProductCommand>.Create()
            .WithValidator(new CreateProductCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CreateProductCommandHandler(productService, commandPipeline);

        var commandResult = await commandHandler.Handle(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 10 });

        Assert.Equal("SKU-1", commandResult.Content.Sku);
        Assert.Equal(10, commandResult.Content.Stock);
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IProductRepository.CreateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithNegativeStock_Should_ThrowValidationFailedForStockAndNotWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithCreateAsync();
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepositoryBuilder.Build()).Build();
        var commandPipeline = CommandPipelineBuilder<CreateProductCommand>.Create().WithValidator(new CreateProductCommandValidator()).Build();
        var commandHandler = new CreateProductCommandHandler(productService, commandPipeline);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = -1 }));

        Assert.Equal([nameof(CreateProductCommand.Stock)], exception.Errors.Keys);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.CreateAsync(It.IsAny<Product>()), Times.Never);
    }
}
