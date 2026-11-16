using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Products;
using Moq;

namespace Boilerplate.Tests.TestSupport.Domain.Products;

public class ProductServiceBuilder
{
    private IProductRepository _productRepository = Mock.Of<IProductRepository>();

    public static ProductServiceBuilder Create() => new();

    public ProductServiceBuilder WithProductRepository(IProductRepository productRepository)
    {
        _productRepository = productRepository;
        return this;
    }

    public IProductService Build()
    {
        return new ProductService(_productRepository);
    }
}
