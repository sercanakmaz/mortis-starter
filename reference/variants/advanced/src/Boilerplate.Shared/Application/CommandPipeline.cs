using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Diagnostics;
using FluentValidation;
using System.Diagnostics;
using System.Net;

namespace Boilerplate.Shared.Application;

public sealed class CommandPipeline<TCommand>(
    IEnumerable<IValidator<TCommand>> validators,
    IEnumerable<ICommandHandlerMiddleware> commandHandlerMiddlewares,
    IUnitOfWork unitOfWork,
    EventContext eventContext) where TCommand : ICommand
{
    public async Task<CommandResult<TContent>> ExecuteAsync<TContent>(TCommand command, Func<TCommand, Task<CommandResult<TContent>>> onHandle)
    {
        using var activity = Telemetry.Source.StartActivity(typeof(TCommand).Name);
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = Telemetry.Succeeded;

        try
        {
            return await RunAsync(command, onHandle);
        }
        catch (Exception exception)
        {
            outcome = exception is ExceptionBase { HttpStatusCode: < HttpStatusCode.InternalServerError } ? Telemetry.Refused : Telemetry.Failed;
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);

            throw;
        }
        finally
        {
            Telemetry.CommandDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, new KeyValuePair<string, object>("command", typeof(TCommand).Name), new KeyValuePair<string, object>("outcome", outcome));
        }
    }

    private async Task<CommandResult<TContent>> RunAsync<TContent>(TCommand command, Func<TCommand, Task<CommandResult<TContent>>> onHandle)
    {
        if (command is not IContextCommand contextCommand)
        {
            return await HandleAsync(command, onHandle);
        }

        contextCommand.Context = CommandContext.Complete(contextCommand.Context);
        contextCommand.Context = contextCommand.Context with { Chain = [.. contextCommand.Context.Chain, typeof(TCommand).Name] };

        if (contextCommand.Context.Chain.Count > LoopGuardConstants.MaxChain)
        {
            throw new LoopDetectedException(contextCommand.Context.Chain);
        }

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
