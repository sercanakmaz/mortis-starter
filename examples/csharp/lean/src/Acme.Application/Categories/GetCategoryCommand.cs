using Acme.Domain.Aggregates.Categories;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Categories;

public record GetCategoryCommand(int Id) : ICommand<CategoryDto>;

public class GetCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    CommandPipeline<GetCategoryCommand> commandPipeline) : BaseCommandHandler<GetCategoryCommand, CategoryDto>(commandPipeline)
{
    protected override async Task<CommandResult<CategoryDto>> OnHandle(GetCategoryCommand command)
    {
        var category = await categoryRepository.FindByIdAsync(command.Id)
            ?? throw new NotFoundException(nameof(Category));

        return CommandResult.FromContent(CategoryDto.Map(category));
    }
}
