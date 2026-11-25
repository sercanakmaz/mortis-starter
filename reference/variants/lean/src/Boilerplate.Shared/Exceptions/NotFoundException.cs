using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class NotFoundException(string name) : ExceptionBase("not_found", HttpStatusCode.NotFound, $"{name} was not found.");
