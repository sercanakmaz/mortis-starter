using Microsoft.EntityFrameworkCore;
using Boilerplate.Domain.Aggregates.Categories;

namespace Boilerplate.Infrastructure.Persistence.Categories;

public class CategoryReader(AppDbContext context) : ICategoryReader
{
    public Task<CategoryView> FindAsync(int id)
    {
        return context.Set<Category>().AsNoTracking()
            .Where(category => category.Id == id)
            .Select(category => new CategoryView(category.Id, category.Name))
            .FirstOrDefaultAsync();
    }
}
