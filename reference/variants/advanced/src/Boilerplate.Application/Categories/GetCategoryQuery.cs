using System.Text.Json.Serialization;
using FluentValidation;
using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Categories;

public record GetCategoryQuery : IQuery<CategoryDto>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public int Id { get; set; }
}

public class GetCategoryQueryHandler(ICategoryReader categoryReader, IEnumerable<IValidator<GetCategoryQuery>> validators) : BaseQueryHandler<GetCategoryQuery, CategoryDto>(validators)
{
    protected override async Task<CategoryDto> OnHandle(GetCategoryQuery query)
    {
        var categoryView = await categoryReader.FindAsync(query.Id) ?? throw new NotFoundException(nameof(Category));

        return CategoryDto.From(categoryView);
    }
}
