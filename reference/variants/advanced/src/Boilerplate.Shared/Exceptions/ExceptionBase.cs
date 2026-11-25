using System.Net;

namespace Boilerplate.Shared.Exceptions;

public abstract class ExceptionBase(string code, HttpStatusCode httpStatusCode, string message, Exception innerException = null, IReadOnlyDictionary<string, object> parameters = null) : Exception(message, innerException)
{
    public IReadOnlyDictionary<string, object> Parameters { get; } = parameters ?? new Dictionary<string, object>();
    public string Code { get; } = code;
    public HttpStatusCode HttpStatusCode { get; } = httpStatusCode;
}
