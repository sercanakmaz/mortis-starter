using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Categories;

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
