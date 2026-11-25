using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class BusinessRuleException(string code, string message) : ExceptionBase(code, HttpStatusCode.Conflict, message);
