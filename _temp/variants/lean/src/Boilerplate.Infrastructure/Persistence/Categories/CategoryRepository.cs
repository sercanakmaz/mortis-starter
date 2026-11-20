using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Categories;

public class CategoryRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Category, int>(context, unitOfWork, eventContext), ICategoryRepository;
