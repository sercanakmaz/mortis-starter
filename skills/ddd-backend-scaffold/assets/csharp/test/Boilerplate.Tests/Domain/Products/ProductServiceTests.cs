using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using Moq;

namespace Boilerplate.Tests.Domain.Products;

public class ProductServiceTests
{
    [Fact]
    public async Task Create_Should_RaiseCreatedBeforeTheRepositoryWriteAndSetTheAudit()
    {
        var eventsAtWrite = new List<EventBase>();
        var productRepository = ProductRepositoryBuilder.Create()
            .WithIsSkuExistsAsync(false)
            .WithCreateAsync(product => eventsAtWrite.AddRange(product.DomainEvents))
            .Build();
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepository).Build();

        var product = await productService.CreateAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), "SKU-1", "Bolt", 10);

        Assert.IsType<Created>(Assert.Single(eventsAtWrite));
        Assert.Equal(10, product.Stock);
        Assert.Equal("test-user-id", product.CreatedUserId);
    }

    [Fact]
    public async Task Create_WithExistingSku_Should_ThrowDuplicateAndNotWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithIsSkuExistsAsync(true).WithCreateAsync();
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepositoryBuilder.Build()).Build();

        var exception = await Assert.ThrowsAsync<DuplicateException>(() => productService.CreateAsync(FakeAuditUser.Create(), Guid.CreateVersion7(), "SKU-1", "Bolt", 10));

        Assert.Equal("Product already exists.", exception.Message);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.CreateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Get_WithNotExistsProduct_Should_ThrowNotFoundForProduct()
    {
        var productService = ProductServiceBuilder.Create().WithProductRepository(ProductRepositoryBuilder.Create().Build()).Build();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => productService.GetAsync(Guid.CreateVersion7()));

        Assert.Equal("Product was not found.", exception.Message);
    }

    [Fact]
    public async Task IncreaseStock_Should_WriteTheProductFoundBySkuWithTheUpdatedAudit()
    {
        Product writtenProduct = null;
        var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();
        var productRepositoryBuilder = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(product)
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct);
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepositoryBuilder.Build()).Build();

        await productService.IncreaseStockAsync(FakeAuditUser.Create(), "SKU-1", 2);

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.FindBySkuTrackingAsync("SKU-1"), Times.Once);
        Assert.Same(product, writtenProduct);
        Assert.Equal(12, writtenProduct.Stock);
        Assert.Equal("test-user-id", writtenProduct.UpdatedUserId);
        Assert.IsType<StockIncreased>(Assert.Single(writtenProduct.DomainEvents));
    }

    [Fact]
    public async Task IncreaseStock_WithNotExistsProduct_Should_ThrowNotFoundAndNotWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithUpdateAsync();
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepositoryBuilder.Build()).Build();

        await Assert.ThrowsAsync<NotFoundException>(() => productService.IncreaseStockAsync(FakeAuditUser.Create(), "SKU-1", 2));

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task DecreaseStock_Should_WriteTheProductWithTheLowerStock()
    {
        Product writtenProduct = null;
        var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();
        var productRepository = ProductRepositoryBuilder.Create()
            .WithFindBySkuTrackingAsync(product)
            .WithUpdateAsync(updatedProduct => writtenProduct = updatedProduct)
            .Build();
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepository).Build();

        await productService.DecreaseStockAsync(FakeAuditUser.Create(), "SKU-1", 4);

        Assert.Equal(6, writtenProduct.Stock);
        Assert.IsType<StockDecreased>(Assert.Single(writtenProduct.DomainEvents));
    }

    [Fact]
    public async Task DecreaseStock_WithMoreThanTheStock_Should_ThrowInsufficientStockAndNotWrite()
    {
        var product = FakeProduct.Create("SKU-1", 3);
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithFindBySkuTrackingAsync(product).WithUpdateAsync();
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepositoryBuilder.Build()).Build();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => productService.DecreaseStockAsync(FakeAuditUser.Create(), "SKU-1", 4));

        Assert.Equal(ProductConstants.InsufficientStockCode, exception.Code);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task DecreaseStock_WithNotExistsProduct_Should_ThrowNotFoundAndNotWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithUpdateAsync();
        var productService = ProductServiceBuilder.Create().WithProductRepository(productRepositoryBuilder.Build()).Build();

        await Assert.ThrowsAsync<NotFoundException>(() => productService.DecreaseStockAsync(FakeAuditUser.Create(), "SKU-1", 1));

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.UpdateAsync(It.IsAny<Product>()), Times.Never);
    }
}
