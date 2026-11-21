# Authentication, C#

Applies when `auth` is `jwt`. Projects `{Name}.Infrastructure` (folder `Authentication/`), `{Name}.Api` (folder `Authentication/`) and `{Name}.Console`. Layer 1: `options/auth.md`.

Callers sign in elsewhere and present a bearer token. The Api validates it and fills the command's context from its claims: the subject and email become the audit user, and with `tenancy.mode` = `interface` the tenant claim becomes `CommandContext.TenantId` (`tenancy.md`). The project issues no tokens and is not an identity provider; for local work the Console writes a signed development token. Permissions (`IAuthorizedCommand`) are not part of this page yet.

## How it fits together

```
Api request -> UseAuthentication: JwtBearer validates the token (issuer, audience, signature, lifetime)
            -> AuthorizeFilter: no valid token -> 401
            -> ContextCommandFilter: CommandContext.User = (sub, email) from the claims named in AuthSettings
                                     with tenancy: CommandContext.TenantId = the tenant claim, and the tenant scope is entered
Console run -> [--token <jwt> | MORTIS_TOKEN] validated the same way; the same claims fill the context
            -> without a token: the console user, and no tenant
dotnet run -- dev-token '{"subject":..., "email":..., "tenant":...}'   Development only: prints a token signed with Auth:SigningKey
```

- Every controller needs an authenticated caller (`AuthorizeFilter` on all controllers). `/health` stays open.
- The claim names are settings (`Auth:SubjectClaim`, `Auth:EmailClaim`, `Auth:TenantClaim`), because identity providers name them differently. Inbound claims are not renamed (`MapInboundClaims = false`).
- Two ways to trust a token, both settings: `Auth:Authority` (an OpenID Connect authority whose published keys sign the tokens) for real environments, `Auth:SigningKey` (a symmetric development key, only in `appsettings.Development.json`) for local work and the conformance checks.
- Event consumers keep running as the system user (`ContextOf(@event)`), whatever token raised the event.

## AuthSettings

File `src/{Name}.Infrastructure/Authentication/AuthSettings.cs`, bound from the `Auth` section by `AddInfrastructureModule` (`services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.Section));`). Shared by the Api and the Console, so both read tokens the same way.

```csharp
public class AuthSettings
```

```csharp
public class AuthSettings
```

```csharp
public const string Section = "Auth";
```

```csharp
public const string MetadataPath = "/.well-known/openid-configuration";
```

```csharp
public string Authority { get; set; }
```

```csharp
public string Issuer { get; set; }
```

```csharp
public string Audience { get; set; }
```

```csharp
public string SigningKey { get; set; }
```

```csharp
public string SubjectClaim { get; set; } = "sub";
```

```csharp
public string EmailClaim { get; set; } = "email";
```

```csharp
public string TenantClaim { get; set; } = "tenant_id";
```

```csharp
public TokenValidationParameters ValidationParameters()
{
    return new TokenValidationParameters
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = string.IsNullOrEmpty(SigningKey) ? null : SigningSecurityKey(),
        ConfigurationManager = string.IsNullOrEmpty(Authority) ? null : new ConfigurationManager<OpenIdConnectConfiguration>(Authority.TrimEnd('/') + MetadataPath, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever()),
        NameClaimType = SubjectClaim
    };
}
```

```csharp
public SymmetricSecurityKey SigningSecurityKey()
{
    return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
}
```

```csharp
public AuditUser UserOf(ClaimsPrincipal principal)
{
    return new AuditUser(principal.FindFirst(SubjectClaim)?.Value, principal.FindFirst(EmailClaim)?.Value);
}
```

```csharp
public string TenantOf(ClaimsPrincipal principal)
{
    return principal.FindFirst(TenantClaim)?.Value;
}
```

- Without tenancy, leave out `TenantClaim` and `TenantOf`.
- With `Authority` set, the signing keys come from the authority's metadata through `ConfigurationManager`; with `SigningKey` set, from the key.

## The Api

File `src/{Name}.Api/Authentication/JwtAuthenticationModule.cs`:

```csharp
public static class JwtAuthenticationModule
```

```csharp
public static class JwtAuthenticationModule
```

```csharp
public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
{
    var authSettings = configuration.GetSection(AuthSettings.Section).Get<AuthSettings>() ?? new AuthSettings();

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = authSettings.ValidationParameters();
        });
    services.AddAuthorization();

    return services;
}
```

`Program.cs` (the whole file is on `hosts.md`) adds the module, the filter on every controller and the two middlewares after the correlation middleware:

```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter());
    options.Filters.Add<ContextCommandFilter>();
});
builder.Services.AddJwtAuthentication(builder.Configuration);
```

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

`ContextCommandFilter` takes `IOptions<AuthSettings>` and reads the user from the configured claims. Without tenancy:

```csharp
public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
{
    var principal = context.HttpContext.User;
    var commandContext = new CommandContext
    {
        User = authSettings.Value.UserOf(principal),
        CorrelationId = context.HttpContext.Items[ApiConstants.CorrelationIdItem] is Guid correlationId ? correlationId : Guid.CreateVersion7()
    };

    foreach (var contextCommand in context.ActionArguments.Values.OfType<IContextCommand>())
    {
        contextCommand.Context = commandContext;
    }

    await next();
}
```

With tenancy it also takes `TenantScope`, sets `TenantId` from `TenantOf(principal)` and enters the tenant scope before the action runs (`tenancy.md`).

Package: `Microsoft.AspNetCore.Authentication.JwtBearer` in the Api, `Microsoft.IdentityModel.Protocols.OpenIdConnect` in Infrastructure (`solution.md` lists the versions).

## The Console

File `src/{Name}.Console/ConsoleTokens.cs`. It turns a token into the run's context, and writes development tokens:

```csharp
public class ConsoleTokens(AuthSettings authSettings, IHostEnvironment hostEnvironment)
```

```csharp
public class ConsoleTokens(AuthSettings authSettings, IHostEnvironment hostEnvironment)
```

```csharp
public async Task<CommandContext> ContextOfAsync(string token)
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return new CommandContext { User = new AuditUser(ConsoleConstants.UserId, ConsoleConstants.UserEmail) };
    }

    var tokenValidationResult = await new JsonWebTokenHandler().ValidateTokenAsync(token, authSettings.ValidationParameters());

    if (!tokenValidationResult.IsValid)
    {
        throw new InvalidParameterException("token", "the token is not valid", tokenValidationResult.Exception);
    }

    var principal = new ClaimsPrincipal(tokenValidationResult.ClaimsIdentity);

    return new CommandContext { User = authSettings.UserOf(principal), TenantId = authSettings.TenantOf(principal) };
}
```

```csharp
public string WriteDevelopmentToken(string argument)
{
    if (!hostEnvironment.IsDevelopment())
    {
        throw new ForbiddenException("Writing a development token outside the Development environment");
    }

    if (string.IsNullOrEmpty(authSettings.SigningKey))
    {
        throw new InvalidParameterException("Auth:SigningKey", "a development signing key is required");
    }

    var developmentUser = JsonSerializer.Deserialize<DevelopmentUser>(string.IsNullOrWhiteSpace(argument) ? "{}" : argument, ConsoleConstants.InputOptions);
    var claims = new Dictionary<string, object>
    {
        [authSettings.SubjectClaim] = developmentUser.Subject ?? ConsoleConstants.UserId,
        [authSettings.EmailClaim] = developmentUser.Email ?? ConsoleConstants.UserEmail
    };

    if (!string.IsNullOrEmpty(developmentUser.Tenant))
    {
        claims[authSettings.TenantClaim] = developmentUser.Tenant;
    }

    return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = authSettings.Issuer,
        Audience = authSettings.Audience,
        Claims = claims,
        Expires = DateTime.UtcNow.AddHours(ConsoleConstants.DevelopmentTokenHours),
        SigningCredentials = new SigningCredentials(authSettings.SigningSecurityKey(), SecurityAlgorithms.HmacSha256)
    });
}
```

```csharp
private sealed record DevelopmentUser(string Subject, string Email, string Tenant);
```

- Without tenancy, leave out the tenant claim and the record's `Tenant`.
- An invalid token fails the run with `invalid_parameter` (exit code 2). `dev-token` outside Development fails with `forbidden`.

`ConsoleCommandRunner.RunAsync` takes the context instead of building the console user itself:

```csharp
public async Task<object> RunAsync(string name, string argument, CommandContext commandContext)
{
    var commandType = commandAssembly.GetTypes()
        .FirstOrDefault(type => !type.IsAbstract && type.Name.Equals(name + ConsoleConstants.CommandSuffix, StringComparison.OrdinalIgnoreCase));

    var contentType = commandType?.GetInterfaces()
        .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>))?
        .GetGenericArguments()[0]
        ?? throw new InvalidParameterException("command", $"{name} is not a command");

    var json = string.IsNullOrWhiteSpace(argument) ? "{}"
        : argument.StartsWith(ConsoleConstants.FilePrefix) ? await File.ReadAllTextAsync(argument[ConsoleConstants.FilePrefix.Length..])
        : argument;

    object command;

    try
    {
        command = JsonSerializer.Deserialize(json, commandType, ConsoleConstants.InputOptions);
    }
    catch (JsonException exception)
    {
        throw new InvalidParameterException("arguments", exception.Message, exception);
    }

    if (command is null)
    {
        throw new InvalidParameterException("arguments", "a JSON object is required");
    }

    if (command is IContextCommand contextCommand)
    {
        contextCommand.Context = commandContext;
    }

    var send = typeof(ICommandDispatcher).GetMethod(nameof(ICommandDispatcher.Send)).MakeGenericMethod(contentType);
    dynamic commandResult = await (dynamic)send.Invoke(commandDispatcher, [command]);

    return commandResult.Content;
}
```

`ConsoleConstants` gains the option, the variable, the command name and the token lifetime:

```csharp
public static class ConsoleConstants
```

```csharp
public const string UserId = "console";
```

```csharp
public const string UserEmail = "console@localhost";
```

```csharp
public const string CommandSuffix = "Command";
```

```csharp
public const string FilePrefix = "@";
```

```csharp
public const string TokenOption = "--token";
```

```csharp
public const string TokenVariable = "MORTIS_TOKEN";
```

```csharp
public const string DevelopmentTokenCommand = "dev-token";
```

```csharp
public const int DevelopmentTokenHours = 8;
```

```csharp
public static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };
```

```csharp
public static readonly JsonSerializerOptions InputOptions = new() { PropertyNameCaseInsensitive = true };
```

`Program.cs` with `auth` = `jwt` (the plain version is on `hosts.md`). The `TenantScope` line is there only with tenancy, and the `AddEventConsumer` lines of `hosts.md` stay when `events.transport` is not `rabbitmq` (this snippet is the RabbitMQ variant's, which has none):

```csharp
using Microsoft.Extensions.DependencyInjection;
using {Name}.Infrastructure.Authentication;
using {Name}.Infrastructure.Tenancy;
using Microsoft.Extensions.Options;
using {Name}.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using {Name}.Shared.Application;
using {Name}.Shared.Exceptions;
using {Name}.Infrastructure;
using {Name}.Application;
using {Name}.Console;
using {Name}.Domain;
using {Name}.Shared;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Logging.AddJsonLogging(toStandardError: true);

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration);

using var host = builder.Build();

var arguments = args.ToList();
var token = Environment.GetEnvironmentVariable(ConsoleConstants.TokenVariable);

if (arguments.Count >= 2 && arguments[0] == ConsoleConstants.TokenOption)
{
    token = arguments[1];
    arguments.RemoveRange(0, 2);
}

if (arguments.Count == 0)
{
    System.Console.Error.WriteLine("Usage: dotnet run -- [--token <jwt>] <Command> [json | @file.json]   or   dotnet run -- dev-token [json]");

    return 1;
}

using var scope = host.Services.CreateScope();

var consoleTokens = new ConsoleTokens(host.Services.GetRequiredService<IOptions<AuthSettings>>().Value, host.Services.GetRequiredService<IHostEnvironment>());
var consoleCommandRunner = new ConsoleCommandRunner(scope.ServiceProvider.GetRequiredService<ICommandDispatcher>(), typeof(ApplicationModule).Assembly);

try
{
    if (arguments[0] == ConsoleConstants.DevelopmentTokenCommand)
    {
        System.Console.WriteLine(consoleTokens.WriteDevelopmentToken(arguments.ElementAtOrDefault(1)));
```

The member continues:

```csharp
        return 0;
    }

    var commandContext = await consoleTokens.ContextOfAsync(token);

    scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(commandContext.TenantId);

    var content = await consoleCommandRunner.RunAsync(arguments[0], arguments.ElementAtOrDefault(1), commandContext);

    System.Console.WriteLine(JsonSerializer.Serialize(content, ConsoleConstants.OutputOptions));

    return 0;
}
catch (ExceptionBase exception)
{
    var errors = (exception as ValidationFailedException)?.Errors;

    System.Console.Error.WriteLine(JsonSerializer.Serialize(new { code = exception.Code, message = exception.Message, errors }));

    return 2;
}
```

The Console project copies `appsettings.Development.json` to its output, next to `appsettings.json`.

## Settings

Both hosts' `appsettings.json` get the `Auth` section (without `TenantClaim` when there is no tenancy):

```json
"Auth": {
  "Authority": "",
  "Issuer": "{database}",
  "Audience": "{database}",
  "SubjectClaim": "sub",
  "EmailClaim": "email",
  "TenantClaim": "tenant_id"
}
```

File `appsettings.Development.json` in the Api and the Console, never in another environment:

```json
{
  "Auth": {
    "SigningKey": "development-only-signing-key-change-me-0123456789"
  }
}
```

The conformance suite runs the hosts with `DOTNET_ENVIRONMENT=Development` so that this file applies.

## Tests

`test/{Name}.Tests/Infrastructure/Authentication/AuthSettingsTests.cs`, with `FakeAuthSettings` in `TestSupport/Infrastructure/Authentication/`:

```csharp
public class AuthSettingsTests
```

```csharp
[Fact]
public void UserOf_And_TenantOf_Should_ReadTheConfiguredClaims()
{
    var authSettings = new AuthSettings { SubjectClaim = "uid", EmailClaim = "mail", TenantClaim = "org" };
    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("uid", "user-1"), new Claim("mail", "user-1@localhost"), new Claim("org", "tenant-1")], "test"));

    var user = authSettings.UserOf(principal);

    Assert.Equal(["user-1", "user-1@localhost", "tenant-1"], [user.Id, user.Email, authSettings.TenantOf(principal)]);
}
```

```csharp
[Fact]
public async Task ValidationParameters_WithASigningKey_Should_AcceptATokenSignedWithItAndRefuseAnother()
{
    var authSettings = FakeAuthSettings.Create();

    var valid = await new JsonWebTokenHandler().ValidateTokenAsync(FakeAuthSettings.TokenSignedWith(FakeAuthSettings.SigningKey), authSettings.ValidationParameters());
    var forged = await new JsonWebTokenHandler().ValidateTokenAsync(FakeAuthSettings.TokenSignedWith(FakeAuthSettings.SigningKey + "-forged"), authSettings.ValidationParameters());

    Assert.True(valid.IsValid);
    Assert.False(forged.IsValid);
}
```

```csharp
public static class FakeAuthSettings
```

```csharp
public const string SigningKey = "unit-test-signing-key-0123456789-abcdefghij";
```

```csharp
public static AuthSettings Create() => new() { Issuer = "issuer", Audience = "audience", SigningKey = SigningKey };
```

```csharp
public static string TokenSignedWith(string signingKey)
{
    return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "issuer",
        Audience = "audience",
        Claims = new Dictionary<string, object> { ["sub"] = "user-1" },
        Expires = DateTime.UtcNow.AddMinutes(5),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256)
    });
}
```

`test/{Name}.Tests/Console/ConsoleTokensTests.cs`:

```csharp
public class ConsoleTokensTests
```

```csharp
[Fact]
public async Task ContextOfAsync_WithoutAToken_Should_BeTheConsoleUserWithoutATenant()
{
    var commandContext = await new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Production }).ContextOfAsync(null);

    Assert.Equal([ConsoleConstants.UserId, ConsoleConstants.UserEmail, null], [commandContext.User.Id, commandContext.User.Email, commandContext.TenantId]);
}
```

```csharp
[Fact]
public async Task WriteDevelopmentToken_InDevelopment_Should_WriteATokenThatContextOfAsyncReadsBack()
{
    var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Development });
    var tenantId = Guid.CreateVersion7().ToString();

    var commandContext = await consoleTokens.ContextOfAsync(consoleTokens.WriteDevelopmentToken($"{{\"subject\":\"user-1\",\"email\":\"user-1@localhost\",\"tenant\":\"{tenantId}\"}}"));

    Assert.Equal(["user-1", "user-1@localhost", tenantId], [commandContext.User.Id, commandContext.User.Email, commandContext.TenantId]);
}
```

```csharp
[Fact]
public void WriteDevelopmentToken_OutsideDevelopment_Should_ThrowForbidden()
{
    var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Production });

    Assert.Throws<ForbiddenException>(() => consoleTokens.WriteDevelopmentToken(null));
}
```

```csharp
[Fact]
public async Task ContextOfAsync_WithATokenThatDoesNotValidate_Should_ThrowInvalidParameterForTheToken()
{
    var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Development });

    var exception = await Assert.ThrowsAsync<InvalidParameterException>(() => consoleTokens.ContextOfAsync("not.a.token"));

    Assert.StartsWith("token is invalid", exception.Message);
}
```

`ContextCommandFilterTests` and `ActionExecutingContextBuilder` use the `sub` and `email` claims; `ConsoleCommandRunnerTests` passes the context (`hosts.md`). The 401, the audit columns and the refusal outside Development are proven by `scripts/conformance/auth.sh`.

## Decision record

`docs/adr/0014-bearer-tokens-fill-the-context.md`, in `project-docs-adr.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Api/Authentication/JwtAuthenticationModule.cs` | `{Name}.Api.Authentication` | Microsoft.AspNetCore.Authentication.JwtBearer, {Name}.Infrastructure.Authentication |
| `src/{Name}.Console/ConsoleTokens.cs` | `{Name}.Console` | Microsoft.Extensions.Hosting, Microsoft.IdentityModel.JsonWebTokens, Microsoft.IdentityModel.Tokens, System.Security.Claims, System.Text.Json, {Name}.Infrastructure.Authentication, {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Shared.Exceptions |
| `src/{Name}.Infrastructure/Authentication/AuthSettings.cs` | `{Name}.Infrastructure.Authentication` | Microsoft.IdentityModel.Protocols, Microsoft.IdentityModel.Protocols.OpenIdConnect, Microsoft.IdentityModel.Tokens, System.Security.Claims, System.Text, {Name}.Shared.Domain |
| `test/{Name}.Tests/Console/ConsoleTokensTests.cs` | `{Name}.Tests.Console` | Microsoft.Extensions.Hosting, Microsoft.Extensions.Hosting.Internal, {Name}.Console, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Infrastructure.Authentication |
| `test/{Name}.Tests/Infrastructure/Authentication/AuthSettingsTests.cs` | `{Name}.Tests.Infrastructure.Authentication` | Microsoft.IdentityModel.JsonWebTokens, System.Security.Claims, {Name}.Infrastructure.Authentication, {Name}.Tests.TestSupport.Infrastructure.Authentication |
| `test/{Name}.Tests/TestSupport/Infrastructure/Authentication/FakeAuthSettings.cs` | `{Name}.Tests.TestSupport.Infrastructure.Authentication` | Microsoft.IdentityModel.JsonWebTokens, Microsoft.IdentityModel.Tokens, System.Text, {Name}.Infrastructure.Authentication |
