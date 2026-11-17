using System.Net;

namespace Acme.Shared.Exceptions;

public class DuplicateException(string name, Exception innerException = null) : ExceptionBase("duplicate", HttpStatusCode.Conflict, $"{name} already exists.", innerException);
