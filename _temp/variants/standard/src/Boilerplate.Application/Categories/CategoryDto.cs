using Boilerplate.Domain.Aggregates.Categories;

namespace Boilerplate.Application.Categories;

public record CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; }

    public static CategoryDto Map(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name
    };
}
