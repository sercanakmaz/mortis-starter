using System.Net;

namespace Boilerplate.Shared.Exceptions;

public class TenantRequiredException(string type) : ExceptionBase("tenant_required", HttpStatusCode.Forbidden, $"{type} belongs to a tenant, and the caller has none.", parameters: new Dictionary<string, object> { ["type"] = type });
