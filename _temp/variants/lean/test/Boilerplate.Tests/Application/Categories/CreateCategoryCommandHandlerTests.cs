using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Domain.Categories;
using Boilerplate.Domain.Aggregates.Categories.Events;
using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Application.Categories;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Application.Categories;

public class CreateCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_RaiseCreatedWithTheIdTheDatabaseAssignedAndHandItOverInTheSameTransaction()
    {
        var calls = new List<string>();
        Category writtenCategory = null;
        var categoryRepository = CategoryRepositoryBuilder.Create()
            .WithCreateAsyncAssigningId(42, calls)
            .WithUpdateAsync(updatedCategory =>
            {
                writtenCategory = updatedCategory;
                calls.Add(nameof(ICategoryRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<CreateCategoryCommand>.Create()
            .WithValidator(new CreateCategoryCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CreateCategoryCommandHandler(categoryRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new CreateCategoryCommand { Name = "Tools" });

        Assert.Equal(42, commandResult.Content.Id);
        Assert.Equal("42", Assert.IsType<Created>(Assert.Single(writtenCategory.DomainEvents)).Id);
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(ICategoryRepository.CreateAsync), nameof(ICategoryRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithoutAName_Should_ThrowValidationFailedForName()
    {
        var commandHandler = new CreateCategoryCommandHandler(CategoryRepositoryBuilder.Create().Build(), CommandPipelineBuilder<CreateCategoryCommand>.Create().WithValidator(new CreateCategoryCommandValidator()).Build());

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new CreateCategoryCommand { Name = "" }));

        Assert.Equal([nameof(CreateCategoryCommand.Name)], exception.Errors.Keys);
    }
}
