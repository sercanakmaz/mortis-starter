namespace Acme.Api.Middlewares;

public static class ApiConstants
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    public const string CorrelationIdItem = "CorrelationId";

    public const string UnexpectedErrorCode = "unexpected_error";

    public const string UnexpectedErrorMessage = "An unexpected error occurred.";

    public const string InvalidRequestCode = "invalid_request";
    public const string InvalidRequestMessage = "The request could not be read.";
    public const string RouteNotFoundCode = "route_not_found";
    public const string RouteNotFoundMessage = "No endpoint answers this address.";
    public const string MethodNotAllowedCode = "method_not_allowed";
    public const string MethodNotAllowedMessage = "This endpoint does not answer this method.";
}
