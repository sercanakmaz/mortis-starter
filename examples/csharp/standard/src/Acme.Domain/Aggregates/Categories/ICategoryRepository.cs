using Acme.Shared.Persistence;

namespace Acme.Domain.Aggregates.Categories;

public interface ICategoryRepository : IRepository<Category, int>;
