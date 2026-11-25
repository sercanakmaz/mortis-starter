using System.Text.RegularExpressions;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Products;

public class ProductReader(IMongoDatabase database) : IProductReader
{
    private readonly IMongoCollection<Product> _products = database.GetCollection<Product>("products");

    public async Task<ProductView> FindAsync(Guid id)
    {
        var product = await _products.Find(candidate => candidate.Id == id).FirstOrDefaultAsync();

        return product is null ? null : AsView(product);
    }

    public async Task<PagedResult<ProductView>> ListAsync(ProductListFilter filter)
    {
        var filtered = Filtered(filter);
        var total = (int)await _products.CountDocumentsAsync(filtered);
        var products = await _products.Find(filtered).Sort(SortOf(filter.Sort)).Skip(PageRequest.Skip(filter.Page, filter.Size)).Limit(filter.Size).ToListAsync();

        return new PagedResult<ProductView>(products.Select(AsView).ToList(), filter.Page, filter.Size, total);
    }

    public static FilterDefinition<Product> Filtered(ProductListFilter filter)
    {
        var filtered = Builders<Product>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(filter.NameContains))
        {
            filtered &= Builders<Product>.Filter.Regex(product => product.Name, new BsonRegularExpression(Regex.Escape(filter.NameContains), "i"));
        }

        if (filter.CategoryId is not null)
        {
            filtered &= Builders<Product>.Filter.Eq(product => product.CategoryId, filter.CategoryId);
        }

        return filtered;
    }

    public static SortDefinition<Product> SortOf(ProductSort sort)
    {
        return sort switch
        {
            ProductSort.NameAscending => Builders<Product>.Sort.Ascending(product => product.Name).Ascending(product => product.Id),
            ProductSort.StockDescending => Builders<Product>.Sort.Descending(product => product.Stock).Ascending(product => product.Id),
            _ => Builders<Product>.Sort.Ascending(product => product.Sku)
        };
    }

    private static ProductView AsView(Product product)
    {
        return new ProductView(product.Id, product.Sku, product.Name, product.Stock, product.CategoryId);
    }
}
