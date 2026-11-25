using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Domain.Aggregates.Orders;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Tests.Infrastructure.Persistence.Products;

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
    public void Model_Should_MakeTheTenantAndTheSkuAnAlternateKeySoOrderLinesCanReferenceIt()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product));

        var key = Assert.Single(entityType.GetKeys(), candidate => !candidate.IsPrimaryKey());

        Assert.Equal([nameof(Product.TenantId), nameof(Product.Sku)], key.Properties.Select(property => property.Name));
    }

    [Fact]
    public void Model_Should_GuardTheStockWithARowVersionAndACheckConstraint()
    {
        var entityType = AppDbContextBuilder.Create().Build().GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Product));

        Assert.True(entityType.FindProperty(PersistenceConstants.RowVersionColumn).IsConcurrencyToken);
        Assert.Equal("stock >= 0", Assert.Single(entityType.GetCheckConstraints()).Sql);
    }

    [Fact]
    public void Model_Should_LetOrderLinesReferenceTheProductSkuOfTheSameTenantThroughARestrictingForeignKey()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(OrderLine));

        var foreignKey = Assert.Single(entityType.GetForeignKeys(), candidate => candidate.PrincipalEntityType.ClrType == typeof(Product));

        Assert.Equal([nameof(OrderLine.TenantId), nameof(OrderLine.Sku)], foreignKey.Properties.Select(property => property.Name));
        Assert.Equal([nameof(Product.TenantId), nameof(Product.Sku)], foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Model_Should_FilterDeletedProductsByANamedFilterNextToTheTenantFilter()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product));

        var filterNames = entityType.GetDeclaredQueryFilters().Select(queryFilter => queryFilter.Key);

        Assert.Equal([PersistenceConstants.TenantFilter, PersistenceConstants.SoftDeleteFilter], filterNames.Order(StringComparer.Ordinal).Reverse());
    }

}
