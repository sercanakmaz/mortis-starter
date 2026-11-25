using Boilerplate.Infrastructure.Authentication;
using Boilerplate.Shared.Application;

namespace Boilerplate.Api.Middlewares;

public static class RequestLanguage
{
    public static string Of(HttpContext httpContext, AuthSettings authSettings)
    {
        var acceptedLanguages = httpContext.Request.GetTypedHeaders().AcceptLanguage
            .OrderByDescending(language => language.Quality ?? 1)
            .Select(language => language.Value.Value);

        return Languages.Resolve(new[] { authSettings.LanguageOf(httpContext.User) }.Concat(acceptedLanguages));
    }
}
