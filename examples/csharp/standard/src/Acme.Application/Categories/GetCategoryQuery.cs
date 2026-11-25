using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Categories;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Categories;

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
