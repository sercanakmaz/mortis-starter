using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;

namespace Boilerplate.Infrastructure.Persistence.Products;

public class ProductReader(AppDbContext context) : IProductReader
{
    private static readonly Expression<Func<Product, ProductView>> AsView = product => new ProductView(product.Id, product.Sku, product.Name, product.Stock, product.CategoryId);

    public Task<ProductView> FindAsync(Guid id)
    {
        return context.Set<Product>().AsNoTracking().Where(product => product.Id == id).Select(AsView).FirstOrDefaultAsync();
    }

    public async Task<PagedResult<ProductView>> ListAsync(ProductListFilter filter)
    {
        var total = await Filtered(filter).CountAsync();
        var items = await Page(filter).ToListAsync();

        return new PagedResult<ProductView>(items, filter.Page, filter.Size, total);
    }

    public IQueryable<ProductView> Page(ProductListFilter filter)
    {
        var filtered = Filtered(filter);

        var sorted = filter.Sort switch
        {
            ProductSort.NameAscending => filtered.OrderBy(product => EF.Property<string>(product, ProductConfiguration.NameSortProperty)).ThenBy(product => product.Id),
            ProductSort.StockDescending => filtered.OrderByDescending(product => product.Stock).ThenBy(product => product.Id),
            _ => filtered.OrderBy(product => product.Sku)
        };

        return sorted.Skip(PageRequest.Skip(filter.Page, filter.Size)).Take(filter.Size).Select(AsView);
    }

    private IQueryable<Product> Filtered(ProductListFilter filter)
    {
        var products = context.Set<Product>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.NameContains))
        {
            products = products.Where(product => EF.Functions.ILike(EF.Property<string>(product, ProductConfiguration.NameSearchProperty), "%" + EscapeLike(filter.NameContains) + "%", "\\"));
        }

        if (filter.CategoryId is not null)
        {
            products = products.Where(product => product.CategoryId == filter.CategoryId);
        }

        return products;
    }

    private static string EscapeLike(string text)
    {
        return text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
