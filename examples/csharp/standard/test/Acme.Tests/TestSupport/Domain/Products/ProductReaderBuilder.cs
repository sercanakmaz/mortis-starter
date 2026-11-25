using Acme.Domain.Aggregates.Products;
using Acme.Shared.Application;
using Moq;

namespace Acme.Tests.TestSupport.Domain.Products;

public class ProductReaderBuilder
{
    private readonly Mock<IProductReader> _mock = new();

    public static ProductReaderBuilder Create() => new();

    public ProductReaderBuilder WithFindAsync(ProductView productView)
    {
        _mock.Setup(productReader => productReader.FindAsync(It.IsAny<Guid>())).ReturnsAsync(productView);
        return this;
    }

    public ProductReaderBuilder WithListAsync(PagedResult<ProductView> page)
    {
        _mock.Setup(productReader => productReader.ListAsync(It.IsAny<ProductListFilter>())).ReturnsAsync(page);
        return this;
    }

    public IProductReader Build() => _mock.Object;

    public Mock<IProductReader> BuildMock() => _mock;
}
