using System.Net;
using Boilerplate.Shared.Application;

namespace Boilerplate.Shared.Exceptions;

public class LoopDetectedException(IReadOnlyList<string> chain) : ExceptionBase(
    LoopGuardConstants.LoopDetectedCode,
    HttpStatusCode.InternalServerError,
    $"Commands triggered each other more than {LoopGuardConstants.MaxChain} times: {string.Join(LoopGuardConstants.Separator, chain)}.",
    null,
    new Dictionary<string, object> { ["chain"] = string.Join(LoopGuardConstants.Separator, chain) });
