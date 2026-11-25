using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Acme.Domain.Aggregates.Categories;
using Acme.Domain.Aggregates.Products;
using Acme.Tests.TestSupport.Infrastructure.Persistence;

namespace Acme.Tests.Infrastructure.Persistence.Categories;

public class CategoryConfigurationTests
{
    [Fact]
    public void Model_Should_LetTheDatabaseGenerateTheCategoryId()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Category));

        Assert.Equal("categories", entityType.GetTableName());
        Assert.Equal(ValueGenerated.OnAdd, entityType.FindProperty(nameof(Category.Id)).ValueGenerated);
    }

    [Fact]
    public void Model_Should_LetAProductReferenceACategoryThroughARestrictingForeignKey()
    {
        var foreignKey = Assert.Single(AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product)).GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Category));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.False(foreignKey.IsRequired);
    }
}
