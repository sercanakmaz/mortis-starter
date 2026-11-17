using Acme.Application.Products;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Exceptions;
using Acme.Shared.Persistence;
using Acme.Tests.TestSupport.Domain.Products;
using Acme.Tests.TestSupport.Shared.Application;
using Acme.Tests.TestSupport.Shared.Persistence;
using Moq;

namespace Acme.Tests.Application.Products;

public class CreateProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnTheCreatedProductInsideATransaction()
    {
        var calls = new List<string>();
        var productRepository = ProductRepositoryBuilder.Create().WithCreateAsync(_ => calls.Add(nameof(IProductRepository.CreateAsync))).Build();
        var commandPipeline = CommandPipelineBuilder<CreateProductCommand>.Create()
            .WithValidator(new CreateProductCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CreateProductCommandHandler(productRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 10 });

        Assert.Equal("SKU-1", commandResult.Content.Sku);
        Assert.Equal(10, commandResult.Content.Stock);
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IProductRepository.CreateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithNegativeStock_Should_ThrowValidationFailedForStockAndNotWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithCreateAsync();
        var commandPipeline = CommandPipelineBuilder<CreateProductCommand>.Create().WithValidator(new CreateProductCommandValidator()).Build();
        var commandHandler = new CreateProductCommandHandler(productRepositoryBuilder.Build(), commandPipeline);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = -1 }));

        Assert.Equal([nameof(CreateProductCommand.Stock)], exception.Errors.Keys);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.CreateAsync(It.IsAny<Product>()), Times.Never);
    }
}
