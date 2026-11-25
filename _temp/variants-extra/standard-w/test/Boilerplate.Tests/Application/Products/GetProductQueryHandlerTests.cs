using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Tests.TestSupport.Domain.Products;
using FluentValidation;

namespace Boilerplate.Tests.Application.Products;

public class GetProductQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_MapTheViewOfTheProduct()
    {
        var productId = Guid.CreateVersion7();
        var queryHandler = new GetProductQueryHandler(ProductReaderBuilder.Create().WithFindAsync(new ProductView(productId, "SKU-1", "Bolt", 7, null)).Build(), Array.Empty<IValidator<GetProductQuery>>());

        var productDto = await queryHandler.Handle(new GetProductQuery { Id = productId });

        Assert.Equal((productId, "SKU-1", "Bolt", 7), (productDto.Id, productDto.Sku, productDto.Name, productDto.Stock));
    }

    [Fact]
    public async Task Handle_WithAnUnknownId_Should_ThrowNotFound()
    {
        var queryHandler = new GetProductQueryHandler(ProductReaderBuilder.Create().WithFindAsync(null).Build(), Array.Empty<IValidator<GetProductQuery>>());

        await Assert.ThrowsAsync<NotFoundException>(() => queryHandler.Handle(new GetProductQuery { Id = Guid.CreateVersion7() }));
    }
}
