namespace Boilerplate.Domain.Aggregates.Categories;

public interface ICategoryReader
{
    Task<CategoryView> FindAsync(int id);
}
