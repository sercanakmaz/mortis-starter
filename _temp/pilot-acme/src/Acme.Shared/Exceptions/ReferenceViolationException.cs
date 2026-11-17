using System.Net;

namespace Acme.Shared.Exceptions;

public class ReferenceViolationException(string constraintName, Exception innerException = null) : ExceptionBase("reference_violation", HttpStatusCode.BadRequest, $"The reference {constraintName} points at a missing record, or the record is still referenced.", innerException);
