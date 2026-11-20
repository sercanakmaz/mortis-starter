using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Persistence;
using FluentValidation;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

public class CommandPipelineBuilder<TCommand> where TCommand : ICommand
{
    private readonly List<IValidator<TCommand>> _validators = [];
    private readonly List<ICommandHandlerMiddleware> _commandHandlerMiddlewares = [];
    private IUnitOfWork _unitOfWork = UnitOfWorkBuilder.Create().Build();
    private EventContext _eventContext = new();

    public static CommandPipelineBuilder<TCommand> Create() => new();

    public CommandPipelineBuilder<TCommand> WithValidator(IValidator<TCommand> validator)
    {
        _validators.Add(validator);
        return this;
    }

    public CommandPipelineBuilder<TCommand> WithCommandHandlerMiddleware(ICommandHandlerMiddleware commandHandlerMiddleware)
    {
        _commandHandlerMiddlewares.Add(commandHandlerMiddleware);
        return this;
    }

    public CommandPipelineBuilder<TCommand> WithUnitOfWork(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        return this;
    }

    public CommandPipelineBuilder<TCommand> WithEventContext(EventContext eventContext)
    {
        _eventContext = eventContext;

        return this;
    }

    public CommandPipeline<TCommand> Build()
    {
        return new CommandPipeline<TCommand>(_validators, _commandHandlerMiddlewares, _unitOfWork, _eventContext);
    }
}
