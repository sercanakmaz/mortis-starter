using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class TransactionRequiredException(string aggregateName) : ExceptionBase("transaction_required", HttpStatusCode.InternalServerError, $"A write to {aggregateName} ran outside a transaction. The command must implement ITransactionalCommand.");
