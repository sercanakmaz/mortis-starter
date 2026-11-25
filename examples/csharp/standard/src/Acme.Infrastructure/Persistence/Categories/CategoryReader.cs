using System.Linq.Expressions;
using Acme.Domain.Aggregates.Categories;
using Microsoft.EntityFrameworkCore;

namespace Acme.Infrastructure.Persistence.Categories;

public class CategoryReader(AppDbContext context) : ICategoryReader
{
    private static readonly Expression<Func<Category, CategoryView>> AsView = category => new CategoryView(category.Id, category.Name);

    public Task<CategoryView> FindAsync(int id)
    {
        return context.Set<Category>().AsNoTracking().Where(category => category.Id == id).Select(AsView).FirstOrDefaultAsync();
    }
}
