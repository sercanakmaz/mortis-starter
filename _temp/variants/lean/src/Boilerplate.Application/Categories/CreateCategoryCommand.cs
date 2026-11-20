using Boilerplate.Domain.Aggregates.Categories;
using System.Text.Json.Serialization;
using Boilerplate.Shared.Application;
using FluentValidation;

namespace Boilerplate.Application.Categories;

public record CreateCategoryCommand : ICommand<CategoryDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
    public string Name { get; set; }
}

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(CategoryConstants.NameMaxLength);
    }
}

public class CreateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    CommandPipeline<CreateCategoryCommand> commandPipeline) : BaseCommandHandler<CreateCategoryCommand, CategoryDto>(commandPipeline)
{
    protected override async Task<CommandResult<CategoryDto>> OnHandle(CreateCategoryCommand command)
    {
        var category = new Category(command.Name, command.Context.User);

        await categoryRepository.CreateAsync(category);

        category.MarkCreated();

        await categoryRepository.UpdateAsync(category);

        return CommandResult.FromContent(CategoryDto.Map(category));
    }
}
