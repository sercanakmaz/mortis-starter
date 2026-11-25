using Acme.Application.Products;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using Acme.Tests.TestSupport.Domain.Products;
using Moq;

namespace Acme.Tests.Application.Products;

public class ListProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_MapTheItemsAndKeepThePaging()
    {
        var page = new PagedResult<ProductView>([new ProductView(Guid.CreateVersion7(), "SKU-1", "Bolt", 5, null)], 2, 10, 11);
        var queryHandler = new ListProductsQueryHandler(ProductReaderBuilder.Create().WithListAsync(page).Build(), [new ListProductsQueryValidator()]);

        var pagedResult = await queryHandler.Handle(new ListProductsQuery { Page = 2, Size = 10 });

        Assert.Equal(("SKU-1", 2, 10, 11), (Assert.Single(pagedResult.Items).Sku, pagedResult.Page, pagedResult.Size, pagedResult.Total));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PageRequest.MaxSize + 1)]
    public async Task Handle_WithAnInvalidSize_Should_ThrowValidationFailedBeforeTheReaderIsCalled(int size)
    {
        var productReaderBuilder = ProductReaderBuilder.Create();
        var queryHandler = new ListProductsQueryHandler(productReaderBuilder.Build(), [new ListProductsQueryValidator()]);

        await Assert.ThrowsAsync<ValidationFailedException>(() => queryHandler.Handle(new ListProductsQuery { Size = size }));

        productReaderBuilder.BuildMock().Verify(productReader => productReader.ListAsync(It.IsAny<ProductListFilter>()), Times.Never);
    }
}
