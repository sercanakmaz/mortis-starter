using System.Net;

namespace Acme.Shared.Exceptions;

public class BusinessRuleException(string code, string message) : ExceptionBase(code, HttpStatusCode.Conflict, message);
