using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Domain.Products;

public class ProductTests
{
    [Fact]
    public void Empty_Constructor_Should_Create()
    {
        Assert.NotNull(FakeProduct.CreateEmpty());
    }

    [Fact]
    public void Constructor_Should_SetTheStockAndRaiseNothing()
    {
        var product = new Product("SKU-1", "Bolt", 10, FakeAuditUser.Create());

        Assert.Equal(10, product.Stock);
        Assert.Equal(0, product.Id);
        Assert.Empty(product.DomainEvents ?? []);
    }

    [Fact]
    public void MarkCreated_WithTheDatabaseId_Should_RaiseCreatedCarryingIt()
    {
        var product = FakeProduct.Create(42, "SKU-1", 10);

        var created = Assert.IsType<ProductCreated>(Assert.Single(product.DomainEvents));
        Assert.Equal("42", created.Id);
        Assert.Equal("SKU-1", created.Sku);
    }

    [Fact]
    public void MarkCreated_BeforeTheDatabaseAssignedAnId_Should_Throw()
    {
        var product = new Product("SKU-1", "Bolt", 10, FakeAuditUser.Create());

        Assert.Throws<InvalidOperationException>(() => product.MarkCreated());
    }

    [Fact]
    public void Constructor_WithNegativeStock_Should_ThrowInvalidParameterForStock()
    {
        var exception = Assert.Throws<InvalidParameterException>(() => new Product("SKU-1", "Bolt", -1, FakeAuditUser.Create()));

        Assert.StartsWith("stock is invalid", exception.Message);
    }

    [Fact]
    public void IncreaseStock_Should_AddTheQuantityAndRaiseStockIncreased()
    {
        var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();

        product.IncreaseStock(3);

        Assert.Equal(13, product.Stock);
        var stockIncreased = Assert.IsType<ProductStockIncreased>(Assert.Single(product.DomainEvents));
        Assert.Equal(product.Id.ToString(), stockIncreased.Id);
        Assert.Equal("SKU-1", stockIncreased.Sku);
        Assert.Equal(3, stockIncreased.Quantity);
        Assert.Equal(13, stockIncreased.Stock);
    }

    [Fact]
    public void IncreaseStock_WithZeroQuantity_Should_ThrowInvalidParameterAndKeepTheStock()
    {
        var product = FakeProduct.Create("SKU-1", 10);

        var exception = Assert.Throws<InvalidParameterException>(() => product.IncreaseStock(0));

        Assert.StartsWith("quantity is invalid", exception.Message);
        Assert.Equal(10, product.Stock);
    }

    [Fact]
    public void DecreaseStock_Should_SubtractTheQuantityAndRaiseStockDecreased()
    {
        var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();

        product.DecreaseStock(4);

        Assert.Equal(6, product.Stock);
        var stockDecreased = Assert.IsType<ProductStockDecreased>(Assert.Single(product.DomainEvents));
        Assert.Equal(4, stockDecreased.Quantity);
        Assert.Equal(6, stockDecreased.Stock);
    }

    [Fact]
    public void DecreaseStock_WithTheWholeStock_Should_LeaveZero()
    {
        var product = FakeProduct.Create("SKU-1", 10);

        product.DecreaseStock(10);

        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void DecreaseStock_WithMoreThanTheStock_Should_ThrowInsufficientStockAndKeepTheStock()
    {
        var product = FakeProduct.Create("SKU-1", 10).WithoutEvents();

        var exception = Assert.Throws<BusinessRuleException>(() => product.DecreaseStock(11));

        Assert.Equal(ProductConstants.InsufficientStockCode, exception.Code);
        Assert.Equal(10, product.Stock);
        Assert.Null(product.DomainEvents?.FirstOrDefault());
    }

    [Fact]
    public void DecreaseStock_WithZeroQuantity_Should_ThrowInvalidParameter()
    {
        var product = FakeProduct.Create("SKU-1", 10);

        var exception = Assert.Throws<InvalidParameterException>(() => product.DecreaseStock(0));

        Assert.StartsWith("quantity is invalid", exception.Message);
    }
}
