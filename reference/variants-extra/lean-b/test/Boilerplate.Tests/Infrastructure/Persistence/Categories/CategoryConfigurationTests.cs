using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;
using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Domain.Aggregates.Products;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Tests.Infrastructure.Persistence.Categories;

public class CategoryConfigurationTests
{
    [Fact]
    public void Model_Should_LetTheDatabaseGenerateTheCategoryId()
    {
        var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Category));

        Assert.Equal("Categories", entityType.GetTableName());
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
