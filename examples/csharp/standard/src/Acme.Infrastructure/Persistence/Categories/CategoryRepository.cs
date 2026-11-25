using Acme.Domain.Aggregates.Categories;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;

namespace Acme.Infrastructure.Persistence.Categories;

public class CategoryRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Category, int>(context, unitOfWork, eventContext), ICategoryRepository;
