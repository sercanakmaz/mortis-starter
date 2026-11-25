using Boilerplate.Domain.Aggregates.Categories;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Categories;

public class CategoryReader(IMongoDatabase database) : ICategoryReader
{
    private readonly IMongoCollection<Category> _categories = database.GetCollection<Category>("categories");

    public async Task<CategoryView> FindAsync(int id)
    {
        var category = await _categories.Find(candidate => candidate.Id == id).FirstOrDefaultAsync();

        return category is null ? null : new CategoryView(category.Id, category.Name);
    }
}
