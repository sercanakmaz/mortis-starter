using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Application;
using Boilerplate.Tests.TestSupport.Domain.Products;
using FluentValidation;

namespace Boilerplate.Tests.Application.Products;

public class GetProductQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_MapTheViewOfTheProduct()
    {
        var productId = Guid.CreateVersion7();
        var queryHandler = new GetProductQueryHandler(ProductReaderBuilder.Create().WithFindAsync(new ProductView(productId, "SKU-1", LocalizedText.Of("en", "Bolt").With("tr", "Cıvata"), 7, null)).Build(), new PassThroughCacheService(), Array.Empty<IValidator<GetProductQuery>>());

        var productDto = await queryHandler.Handle(new GetProductQuery { Id = productId, Context = new CommandContext { Language = "tr" } });

        Assert.Equal((productId, "SKU-1", "Cıvata", 7), (productDto.Id, productDto.Sku, productDto.Name, productDto.Stock));
    }

    [Fact]
    public async Task Handle_WithAnUnknownId_Should_ThrowNotFound()
    {
        var queryHandler = new GetProductQueryHandler(ProductReaderBuilder.Create().WithFindAsync(null).Build(), new PassThroughCacheService(), Array.Empty<IValidator<GetProductQuery>>());

        await Assert.ThrowsAsync<NotFoundException>(() => queryHandler.Handle(new GetProductQuery { Id = Guid.CreateVersion7() }));
    }
}
