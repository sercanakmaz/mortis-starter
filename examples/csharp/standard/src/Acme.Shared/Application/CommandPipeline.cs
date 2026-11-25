using Acme.Shared.Domain;
using Acme.Shared.Exceptions;
using Acme.Shared.Persistence;
using FluentValidation;

namespace Acme.Shared.Application;

public sealed class CommandPipeline<TCommand>(
    IEnumerable<IValidator<TCommand>> validators,
    IEnumerable<ICommandHandlerMiddleware> commandHandlerMiddlewares,
    IUnitOfWork unitOfWork,
    EventContext eventContext) where TCommand : ICommand
{
    public async Task<CommandResult<TContent>> ExecuteAsync<TContent>(TCommand command, Func<TCommand, Task<CommandResult<TContent>>> onHandle)
    {
        if (command is not IContextCommand contextCommand)
        {
            return await HandleAsync(command, onHandle);
        }

        contextCommand.Context = CommandContext.Complete(contextCommand.Context);
        eventContext.EnterCommand(contextCommand.Context);

        try
        {
            return await HandleAsync(command, onHandle);
        }
        finally
        {
            eventContext.LeaveCommand();
        }
    }

    private async Task<CommandResult<TContent>> HandleAsync<TContent>(TCommand command, Func<TCommand, Task<CommandResult<TContent>>> onHandle)
    {
        var validationFailures = validators
            .SelectMany(validator => validator.Validate(command).Errors)
            .ToList();

        if (validationFailures.Count > 0)
        {
            throw new ValidationFailedException(validationFailures
                .GroupBy(validationFailure => validationFailure.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(validationFailure => validationFailure.ErrorMessage).ToArray()));
        }

        foreach (var commandHandlerMiddleware in commandHandlerMiddlewares)
        {
            commandHandlerMiddleware.Before(command);
        }

        CommandResult<TContent> commandResult;

        if (command is ITransactionalCommand)
        {
            var transactionKey = typeof(TCommand).FullName;

            try
            {
                await unitOfWork.BeginTransactionAsync(transactionKey);

                commandResult = await onHandle(command);

                await unitOfWork.CommitTransactionAsync(transactionKey);
            }
            catch (Exception)
            {
                await unitOfWork.RollbackTransactionAsync();

                throw;
            }
        }
        else
        {
            commandResult = await onHandle(command);
        }

        foreach (var commandHandlerMiddleware in commandHandlerMiddlewares)
        {
            commandHandlerMiddleware.After(command, commandResult);
        }

        return commandResult;
    }
}
