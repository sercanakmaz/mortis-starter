using Acme.Domain.Aggregates.Categories;

namespace Acme.Application.Categories;

public record CategoryDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public static CategoryDto Map(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name
    };

    public static CategoryDto From(CategoryView view) => new()
    {
        Id = view.Id,
        Name = view.Name
    };
}
