using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Shared.Domain;
using Moq;

namespace Boilerplate.Tests.TestSupport.Domain.Categories;

public class CategoryRepositoryBuilder
{
    private readonly Mock<ICategoryRepository> _mock = new();

    public static CategoryRepositoryBuilder Create() => new();

    public CategoryRepositoryBuilder WithCreateAsyncAssigningId(int id, List<string> calls)
    {
        _mock.Setup(categoryRepository => categoryRepository.CreateAsync(It.IsAny<Category>()))
            .Callback<Category>(category =>
            {
                typeof(Entity<int>).GetProperty(nameof(Entity<int>.Id)).SetValue(category, id);
                calls.Add(nameof(ICategoryRepository.CreateAsync));
            })
            .Returns(Task.CompletedTask);

        return this;
    }

    public CategoryRepositoryBuilder WithUpdateAsync(Action<Category> callback)
    {
        _mock.Setup(categoryRepository => categoryRepository.UpdateAsync(It.IsAny<Category>()))
            .Callback<Category>(category => callback(category))
            .Returns(Task.CompletedTask);

        return this;
    }

    public ICategoryRepository Build() => _mock.Object;
}
