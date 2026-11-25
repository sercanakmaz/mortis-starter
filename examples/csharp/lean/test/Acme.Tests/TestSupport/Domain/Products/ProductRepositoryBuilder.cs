using Moq;
using Acme.Domain.Aggregates.Products;

namespace Acme.Tests.TestSupport.Domain.Products;

public class ProductRepositoryBuilder
{
    private readonly Mock<IProductRepository> _mock = new();

    public static ProductRepositoryBuilder Create() => new();

    public ProductRepositoryBuilder WithFindByIdAsync(Product product)
    {
        _mock.Setup(productRepository => productRepository.FindByIdAsync(It.IsAny<Guid>())).ReturnsAsync(product);
        return this;
    }

    public ProductRepositoryBuilder WithFindByIdTrackingAsync(Product product)
    {
        _mock.Setup(productRepository => productRepository.FindByIdTrackingAsync(It.IsAny<Guid>())).ReturnsAsync(product);
        return this;
    }

    public ProductRepositoryBuilder WithCreateAsync(Action<Product> callback = null)
    {
        _mock.Setup(productRepository => productRepository.CreateAsync(It.IsAny<Product>()))
            .Callback<Product>(product => callback?.Invoke(product))
            .Returns(Task.CompletedTask);
        return this;
    }

    public ProductRepositoryBuilder WithUpdateAsync(Action<Product> callback = null)
    {
        _mock.Setup(productRepository => productRepository.UpdateAsync(It.IsAny<Product>()))
            .Callback<Product>(product => callback?.Invoke(product))
            .Returns(Task.CompletedTask);
        return this;
    }

    public IProductRepository Build() => _mock.Object;

    public Mock<IProductRepository> BuildMock() => _mock;

    public ProductRepositoryBuilder WithFindBySkuTrackingAsync(Product product)
    {
        _mock.Setup(productRepository => productRepository.FindBySkuTrackingAsync(It.IsAny<string>())).ReturnsAsync(product);
        return this;
    }
}
