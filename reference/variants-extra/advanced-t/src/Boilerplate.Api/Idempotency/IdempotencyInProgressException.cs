using System.Net;
using Boilerplate.Infrastructure.Persistence.Idempotency;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Api.Idempotency;

public class IdempotencyInProgressException() : ExceptionBase(IdempotencyConstants.InProgressCode, HttpStatusCode.Conflict, "A request with this idempotency key is still running.");
