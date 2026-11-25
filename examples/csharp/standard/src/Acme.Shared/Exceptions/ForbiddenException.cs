using System.Net;

namespace Acme.Shared.Exceptions;

public class ForbiddenException(string action) : ExceptionBase("forbidden", HttpStatusCode.Forbidden, $"{action} is not allowed.");
