using System.Net;

namespace Boilerplate.Shared.Exceptions;

public abstract class ExceptionBase(string code, HttpStatusCode httpStatusCode, string message, Exception innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public HttpStatusCode HttpStatusCode { get; } = httpStatusCode;
}
