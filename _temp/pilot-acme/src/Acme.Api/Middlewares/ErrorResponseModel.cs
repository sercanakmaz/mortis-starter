namespace Acme.Api.Middlewares;

public record ErrorResponseModel(string Code, string Message, IDictionary<string, string[]> Errors = null);
