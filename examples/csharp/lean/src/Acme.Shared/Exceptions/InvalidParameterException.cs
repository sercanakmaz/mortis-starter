using System.Net;

namespace Acme.Shared.Exceptions;

public class InvalidParameterException(string name, string reason, Exception innerException = null) : ExceptionBase("invalid_parameter", HttpStatusCode.BadRequest, $"{name} is invalid: {reason}", innerException);
