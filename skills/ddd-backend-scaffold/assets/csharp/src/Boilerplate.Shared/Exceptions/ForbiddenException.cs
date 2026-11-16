using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class ForbiddenException(string action) : ExceptionBase("forbidden", HttpStatusCode.Forbidden, $"{action} is not allowed.");
