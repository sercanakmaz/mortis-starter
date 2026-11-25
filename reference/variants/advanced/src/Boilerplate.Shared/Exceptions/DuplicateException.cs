using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class DuplicateException(string name, Exception innerException = null) : ExceptionBase("duplicate", HttpStatusCode.Conflict, $"{name} already exists.", innerException, new Dictionary<string, object> { ["name"] = name });
