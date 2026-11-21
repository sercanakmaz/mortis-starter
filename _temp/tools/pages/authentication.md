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

@@decl variant:advanced/src/Boilerplate.Infrastructure/Authentication/AuthSettings.cs::AuthSettings@@

@@members variant:advanced/src/Boilerplate.Infrastructure/Authentication/AuthSettings.cs::AuthSettings@@

- Without tenancy, leave out `TenantClaim` and `TenantOf`.
- With `Authority` set, the signing keys come from the authority's metadata through `ConfigurationManager`; with `SigningKey` set, from the key.

## The Api

File `src/{Name}.Api/Authentication/JwtAuthenticationModule.cs`:

@@decl src/Boilerplate.Api/Authentication/JwtAuthenticationModule.cs::JwtAuthenticationModule@@

@@members src/Boilerplate.Api/Authentication/JwtAuthenticationModule.cs::JwtAuthenticationModule@@

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

@@member variant:standard/src/Boilerplate.Api/Filters/ContextCommandFilter.cs::ContextCommandFilter::OnActionExecutionAsync@@

With tenancy it also takes `TenantScope`, sets `TenantId` from `TenantOf(principal)` and enters the tenant scope before the action runs (`tenancy.md`).

Package: `Microsoft.AspNetCore.Authentication.JwtBearer` in the Api, `Microsoft.IdentityModel.Protocols.OpenIdConnect` in Infrastructure (`solution.md` lists the versions).

## The Console

File `src/{Name}.Console/ConsoleTokens.cs`. It turns a token into the run's context, and writes development tokens:

@@decl variant:advanced/src/Boilerplate.Console/ConsoleTokens.cs::ConsoleTokens@@

@@members variant:advanced/src/Boilerplate.Console/ConsoleTokens.cs::ConsoleTokens@@

- Without tenancy, leave out the tenant claim and the record's `Tenant`.
- An invalid token fails the run with `invalid_parameter` (exit code 2). `dev-token` outside Development fails with `forbidden`.

`ConsoleCommandRunner.RunAsync` takes the context instead of building the console user itself:

@@member variant:advanced/src/Boilerplate.Console/ConsoleCommandRunner.cs::ConsoleCommandRunner::RunAsync@@

`ConsoleConstants` gains the option, the variable, the command name and the token lifetime:

@@members variant:advanced/src/Boilerplate.Console/ConsoleConstants.cs::ConsoleConstants@@

`Program.cs` with `auth` = `jwt` (the plain version is on `hosts.md`). The `TenantScope` line is there only with tenancy, and the `AddEventConsumer` lines of `hosts.md` stay when `events.transport` is not `rabbitmq` (this snippet is the RabbitMQ variant's, which has none):

@@data variant:advanced/src/Boilerplate.Console/Program.cs csharp@@

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

@@data src/Boilerplate.Api/appsettings.Development.json json@@

The conformance suite runs the hosts with `DOTNET_ENVIRONMENT=Development` so that this file applies.

## Tests

`test/{Name}.Tests/Infrastructure/Authentication/AuthSettingsTests.cs`, with `FakeAuthSettings` in `TestSupport/Infrastructure/Authentication/`:

@@members variant:advanced/test/Boilerplate.Tests/Infrastructure/Authentication/AuthSettingsTests.cs::AuthSettingsTests@@

@@members test/Boilerplate.Tests/TestSupport/Infrastructure/Authentication/FakeAuthSettings.cs::FakeAuthSettings@@

`test/{Name}.Tests/Console/ConsoleTokensTests.cs`:

@@members variant:advanced/test/Boilerplate.Tests/Console/ConsoleTokensTests.cs::ConsoleTokensTests@@

`ContextCommandFilterTests` and `ActionExecutingContextBuilder` use the `sub` and `email` claims; `ConsoleCommandRunnerTests` passes the context (`hosts.md`). The 401, the audit columns and the refusal outside Development are proven by `scripts/conformance/auth.sh`.

## Decision record

`docs/adr/0014-bearer-tokens-fill-the-context.md`, in `project-docs-adr.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings authentication.md@@
