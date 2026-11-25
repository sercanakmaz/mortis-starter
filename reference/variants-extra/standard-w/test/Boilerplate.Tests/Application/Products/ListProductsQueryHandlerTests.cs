using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Moq;

namespace Boilerplate.Tests.Application.Products;

public class ListProductsQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_PassTheFilterAndMapThePage()
    {
        var productReaderBuilder = ProductReaderBuilder.Create().WithListAsync(new PagedResult<ProductView>([new ProductView(Guid.CreateVersion7(), "SKU-1", "Bolt", 7, null)], 2, 10, 11));
        var queryHandler = new ListProductsQueryHandler(productReaderBuilder.Build(), [new ListProductsQueryValidator()]);

        var page = await queryHandler.Handle(new ListProductsQuery { Page = 2, Size = 10, Sort = ProductSort.NameAscending, NameContains = "bo" });

        Assert.Equal((2, 10, 11, "SKU-1"), (page.Page, page.Size, page.Total, page.Items.Single().Sku));
        productReaderBuilder.BuildMock().Verify(productReader => productReader.ListAsync(new ProductListFilter(2, 10, ProductSort.NameAscending, "bo", null)), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PageRequest.MaxSize + 1)]
    public async Task Handle_WithASizeOutOfRange_Should_FailValidationBeforeAnyRead(int size)
    {
        var productReaderBuilder = ProductReaderBuilder.Create();
        var queryHandler = new ListProductsQueryHandler(productReaderBuilder.Build(), [new ListProductsQueryValidator()]);

        await Assert.ThrowsAsync<ValidationFailedException>(() => queryHandler.Handle(new ListProductsQuery { Size = size }));

        productReaderBuilder.BuildMock().Verify(productReader => productReader.ListAsync(It.IsAny<ProductListFilter>()), Times.Never);
    }
}
