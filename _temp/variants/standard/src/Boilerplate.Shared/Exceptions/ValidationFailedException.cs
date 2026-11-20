using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class ValidationFailedException(IDictionary<string, string[]> errors) : ExceptionBase("validation_failed", HttpStatusCode.UnprocessableEntity, "The request is not valid.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
