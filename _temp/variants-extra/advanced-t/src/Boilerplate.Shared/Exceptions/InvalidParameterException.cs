using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class InvalidParameterException(string name, string reason, Exception innerException = null) : ExceptionBase("invalid_parameter", HttpStatusCode.BadRequest, $"{name} is invalid: {reason}", innerException, new Dictionary<string, object> { ["name"] = name, ["reason"] = reason });
