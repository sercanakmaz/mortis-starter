using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Acme.Domain.Aggregates.Orders;
using Acme.Domain.Aggregates.Products;
using Acme.Infrastructure.Persistence;
using Acme.Tests.TestSupport.Infrastructure.Persistence;

namespace Acme.Tests.Infrastructure.Persistence.Products;

public class ProductConfigurationTests
{
    [Fact]
    public void Model_Should_MapProductsToASnakeCaseTable()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product));

        Assert.Equal("products", entityType.GetTableName());
        Assert.Equal("stock", entityType.FindProperty(nameof(Product.Stock)).GetColumnName());
    }

    [Fact]
    public void Model_Should_MakeTheSkuAnAlternateKeySoOrderLinesCanReferenceIt()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product));

        var key = Assert.Single(entityType.GetKeys(), candidate => !candidate.IsPrimaryKey());

        Assert.Equal(nameof(Product.Sku), Assert.Single(key.Properties).Name);
    }

    [Fact]
    public void Model_Should_GuardTheStockWithARowVersionAndACheckConstraint()
    {
        var entityType = AppDbContextBuilder.Create().Build().GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Product));

        Assert.True(entityType.FindProperty(PersistenceConstants.RowVersionColumn).IsConcurrencyToken);
        Assert.Equal("stock >= 0", Assert.Single(entityType.GetCheckConstraints()).Sql);
    }

    [Fact]
    public void Model_Should_LetOrderLinesReferenceTheProductSkuThroughARestrictingForeignKey()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(OrderLine));

        var foreignKey = Assert.Single(entityType.GetForeignKeys(), candidate => candidate.PrincipalEntityType.ClrType == typeof(Product));

        Assert.Equal(nameof(OrderLine.Sku), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(nameof(Product.Sku), Assert.Single(foreignKey.PrincipalKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}
