using Acme.Application.Products;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Exceptions;
using Acme.Tests.TestSupport.Domain.Products;

namespace Acme.Tests.Application.Products;

public class GetProductQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_MapTheViewToTheDto()
    {
        var productView = new ProductView(Guid.CreateVersion7(), "SKU-1", "Bolt", 5, null);
        var queryHandler = new GetProductQueryHandler(ProductReaderBuilder.Create().WithFindAsync(productView).Build(), []);

        var productDto = await queryHandler.Handle(new GetProductQuery { Id = productView.Id });

        Assert.Equal(("SKU-1", "Bolt", 5), (productDto.Sku, productDto.Name, productDto.Stock));
    }

    [Fact]
    public async Task Handle_WithAnUnknownProduct_Should_ThrowNotFound()
    {
        var queryHandler = new GetProductQueryHandler(ProductReaderBuilder.Create().WithFindAsync(null).Build(), []);

        await Assert.ThrowsAsync<NotFoundException>(() => queryHandler.Handle(new GetProductQuery { Id = Guid.CreateVersion7() }));
    }
}
