# The solution, C#

The projects, how they reference each other, the build guards and the pinned versions. Files on this page that are data (props, json, yml, txt) are written out in full: copy them, replacing `{Name}`.

## Create it with the tools

Run these, in this order, in an empty folder named `{Name}`. The tools write the project files; never type a `.csproj` from memory.

```bash
dotnet new sln --format slnx --name {Name}
dotnet new classlib --output src/{Name}.Shared --name {Name}.Shared
dotnet new classlib --output src/{Name}.Domain --name {Name}.Domain
dotnet new classlib --output src/{Name}.Application --name {Name}.Application
dotnet new classlib --output src/{Name}.Infrastructure --name {Name}.Infrastructure
dotnet new webapi --use-controllers --output src/{Name}.Api --name {Name}.Api
dotnet new console --output src/{Name}.Console --name {Name}.Console
dotnet new xunit --output test/{Name}.Tests --name {Name}.Tests
rm -f src/*/Class1.cs test/*/UnitTest1.cs src/{Name}.Api/{Name}.Api.http src/{Name}.Api/WeatherForecast.cs src/{Name}.Api/Controllers/WeatherForecastController.cs src/{Name}.Api/appsettings.Development.json src/{Name}.Api/Properties/launchSettings.json
dotnet sln add src/{Name}.Shared src/{Name}.Domain src/{Name}.Application src/{Name}.Infrastructure src/{Name}.Api src/{Name}.Console test/{Name}.Tests
```

Then overwrite every `.csproj` with the content below. The tools' versions of these files carry `TargetFramework` and package versions that `Directory.Build.props` and `Directory.Packages.props` own.

## Project references

| Project | References | Packages |
|---|---|---|
| `{Name}.Shared` | none | `FluentValidation`, `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `{Name}.Domain` | Shared | none |
| `{Name}.Application` | Domain, Shared | none |
| `{Name}.Infrastructure` | Domain, Shared | `EFCore.NamingConventions`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` (private), `Microsoft.EntityFrameworkCore.Relational`, `Npgsql.EntityFrameworkCore.PostgreSQL` |
| `{Name}.Api` | Application, Infrastructure | `Microsoft.AspNetCore.OpenApi` |
| `{Name}.Console` | Application, Infrastructure | `Microsoft.Extensions.Hosting` |
| `{Name}.Tests` | Api, Application, Console, Infrastructure | `Microsoft.Extensions.DependencyInjection`, `Microsoft.NET.Test.Sdk`, `Moq`, `xunit`, `xunit.runner.visualstudio` (private) |

Domain and Application never reference Infrastructure. Nothing references a host.

## Project files

`src/{Name}.Shared/{Name}.Shared.csproj`:

@@data src/Boilerplate.Shared/Boilerplate.Shared.csproj xml@@

`src/{Name}.Domain/{Name}.Domain.csproj`:

@@data src/Boilerplate.Domain/Boilerplate.Domain.csproj xml@@

`src/{Name}.Application/{Name}.Application.csproj`:

@@data src/Boilerplate.Application/Boilerplate.Application.csproj xml@@

`src/{Name}.Infrastructure/{Name}.Infrastructure.csproj`:

@@data src/Boilerplate.Infrastructure/Boilerplate.Infrastructure.csproj xml@@

`src/{Name}.Api/{Name}.Api.csproj`:

@@data src/Boilerplate.Api/Boilerplate.Api.csproj xml@@

`src/{Name}.Console/{Name}.Console.csproj`:

@@data src/Boilerplate.Console/Boilerplate.Console.csproj xml@@

`test/{Name}.Tests/{Name}.Tests.csproj`:

@@data test/Boilerplate.Tests/Boilerplate.Tests.csproj xml@@

## Root files

`{Name}.slnx` after the `dotnet sln add` above:

@@data Boilerplate.slnx xml@@

`global.json` pins the SDK:

@@data global.json json@@

`Directory.Build.props` sets the framework for every project and turns on the banned-API analyzer:

@@data Directory.Build.props xml@@

`Directory.Packages.props` pins every package. These are the versions the examples were verified against; do not change one without regenerating the examples.

@@data Directory.Packages.props xml@@

`BannedSymbols.txt` makes these calls build errors:

@@data BannedSymbols.txt@@

`.editorconfig` turns a banned call into an error and keeps namespaces file-scoped:

@@data .editorconfig ini@@

`.gitignore`:

@@data .gitignore@@

## Container files

`Dockerfile`:

@@data Dockerfile dockerfile@@

`docker-compose.yml`. `{database}` is `{Name}` in lower case with dots removed. The same name goes in both `appsettings.json` files and in `PersistenceConstants.DesignTimeConnectionString`.

@@data docker-compose.yml yaml@@

## Rules

- `Nullable` is disabled and `ImplicitUsings` enabled, for every project, by `Directory.Build.props`. A project file never repeats them.
- No package without a version line in `Directory.Packages.props`; no version attribute in a project file.
- No package outside this list. A new dependency needs a decision record first.
- The build must end with `0 Warning(s)` and `0 Error(s)`.

