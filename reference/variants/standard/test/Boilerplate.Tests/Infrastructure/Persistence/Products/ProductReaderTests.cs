using System.Text.RegularExpressions;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Infrastructure.Persistence.Products;
using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Tests.Infrastructure.Persistence.Products;

public class ProductReaderTests
{
    [Fact]
    public void Page_Should_SelectOnlyTheViewColumnsWithTheFilterSortAndPageInOneStatement()
    {
        var productReader = new ProductReader(AppDbContextBuilder.Create().Build());

        var sql = productReader.Page(new ProductListFilter(2, 10, ProductSort.NameAscending, "bolt", null)).ToQueryString();

        Assert.Contains("ILIKE", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("created_user_id", sql);
        Assert.Single(Regex.Matches(sql, @"\bSELECT\b"));
    }
}
