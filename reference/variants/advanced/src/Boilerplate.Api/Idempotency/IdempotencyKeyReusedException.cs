using System.Net;
using Boilerplate.Infrastructure.Persistence.Idempotency;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Api.Idempotency;

public class IdempotencyKeyReusedException() : ExceptionBase(IdempotencyConstants.KeyReusedCode, HttpStatusCode.UnprocessableEntity, "This idempotency key was used for another request.");
