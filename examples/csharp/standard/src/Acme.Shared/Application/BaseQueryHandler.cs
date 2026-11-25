using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Shared.Application;

public abstract class BaseQueryHandler<TQuery, TResult>(IEnumerable<IValidator<TQuery>> validators) where TQuery : IQuery<TResult>
{
    public async Task<TResult> Handle(TQuery query)
    {
        query.Context ??= new CommandContext();

        var validationFailures = validators.SelectMany(validator => validator.Validate(query).Errors).ToList();

        if (validationFailures.Count > 0)
        {
            throw new ValidationFailedException(validationFailures
                .GroupBy(validationFailure => validationFailure.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(validationFailure => validationFailure.ErrorMessage).ToArray()));
        }

        return await OnHandle(query);
    }

    protected abstract Task<TResult> OnHandle(TQuery query);
}
