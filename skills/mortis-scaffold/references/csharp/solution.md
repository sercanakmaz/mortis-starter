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
| `{Name}.Infrastructure` | Domain, Shared | `EFCore.NamingConventions` (only with `naming.database` = `snake_case`), `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` (private), `Microsoft.EntityFrameworkCore.Relational`, `Microsoft.Extensions.Hosting`, `Npgsql.EntityFrameworkCore.PostgreSQL` |
| `{Name}.Api` | Application, Infrastructure | `Microsoft.AspNetCore.OpenApi` |
| `{Name}.Console` | Application, Infrastructure | `Microsoft.Extensions.Hosting` |
| `{Name}.Tests` | Api, Application, Console, Infrastructure | `Microsoft.Extensions.DependencyInjection`, `Microsoft.NET.Test.Sdk`, `Moq`, `xunit`, `xunit.runner.visualstudio` (private) |

Domain and Application never reference Infrastructure. Nothing references a host.

## Project files

`src/{Name}.Shared/{Name}.Shared.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
</Project>
```

`src/{Name}.Domain/{Name}.Domain.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../{Name}.Shared/{Name}.Shared.csproj" />
  </ItemGroup>
</Project>
```

`src/{Name}.Application/{Name}.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../{Name}.Domain/{Name}.Domain.csproj" />
    <ProjectReference Include="../{Name}.Shared/{Name}.Shared.csproj" />
  </ItemGroup>
</Project>
```

`src/{Name}.Infrastructure/{Name}.Infrastructure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../{Name}.Domain/{Name}.Domain.csproj" />
    <ProjectReference Include="../{Name}.Shared/{Name}.Shared.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
</Project>
```

`src/{Name}.Api/{Name}.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="../{Name}.Application/{Name}.Application.csproj" />
    <ProjectReference Include="../{Name}.Infrastructure/{Name}.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
  </ItemGroup>
</Project>
```

`src/{Name}.Console/{Name}.Console.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../{Name}.Application/{Name}.Application.csproj" />
    <ProjectReference Include="../{Name}.Infrastructure/{Name}.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
  </ItemGroup>
  <ItemGroup>
    <None Update="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

`test/{Name}.Tests/{Name}.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/{Name}.Api/{Name}.Api.csproj" />
    <ProjectReference Include="../../src/{Name}.Application/{Name}.Application.csproj" />
    <ProjectReference Include="../../src/{Name}.Console/{Name}.Console.csproj" />
    <ProjectReference Include="../../src/{Name}.Infrastructure/{Name}.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

## Root files

`{Name}.slnx` after the `dotnet sln add` above:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/{Name}.Api/{Name}.Api.csproj" />
    <Project Path="src/{Name}.Application/{Name}.Application.csproj" />
    <Project Path="src/{Name}.Console/{Name}.Console.csproj" />
    <Project Path="src/{Name}.Domain/{Name}.Domain.csproj" />
    <Project Path="src/{Name}.Infrastructure/{Name}.Infrastructure.csproj" />
    <Project Path="src/{Name}.Shared/{Name}.Shared.csproj" />
  </Folder>
  <Folder Name="/test/">
    <Project Path="test/{Name}.Tests/{Name}.Tests.csproj" />
  </Folder>
</Solution>
```

`global.json` pins the SDK:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

`Directory.Build.props` sets the framework for every project and turns on the banned-API analyzer:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />
  </ItemGroup>
</Project>
```

`Directory.Packages.props` pins every package. These are the versions the examples were verified against; do not change one without regenerating the examples. Generate only the lines of the chosen options: `EFCore.NamingConventions` (here and in the Infrastructure project file) only with `naming.database` = `snake_case` and `database` = `postgres`; the EF Core and Npgsql packages only with `database` = `postgres` and `MongoDB.Driver` only with `mongo` (`infrastructure-mongo.md`); `RabbitMQ.Client` with `events.transport` = `rabbitmq`; with `auth` = `jwt`, `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.15, referenced by the Api) and `Microsoft.IdentityModel.Protocols.OpenIdConnect` (8.19.2, referenced by Infrastructure).

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="FluentValidation" Version="12.2.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.15" />
    <PackageVersion Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.15" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.15" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.15" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.15" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.15" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.15" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.12.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageVersion Include="EFCore.NamingConventions" Version="10.0.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

`BannedSymbols.txt` makes these calls build errors:

```
M:System.Guid.NewGuid;Use Guid.CreateVersion7(): ids are time-sortable everywhere in this solution.
P:System.DateTime.Now;Use DateTime.UtcNow. Every timestamp is UTC; server-local time differs per node.
P:System.DateTime.Today;Use DateTime.UtcNow.Date.
P:System.DateTimeOffset.Now;Use DateTimeOffset.UtcNow.
P:System.Threading.Tasks.Task`1.Result;Await the task. Blocking deadlocks under load and hides the exception in an AggregateException.
M:System.Threading.Tasks.Task.Wait;Await the task instead of blocking.
```

`.editorconfig` turns a banned call into an error and keeps namespaces file-scoped:

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
indent_style = space
indent_size = 4
trim_trailing_whitespace = true
insert_final_newline = true

[*.{yml,yaml,json,xml,csproj,props,targets,slnx}]
indent_size = 2

[*.cs]
dotnet_diagnostic.RS0030.severity = error
csharp_style_namespace_declarations = file_scoped:warning
```

`.gitignore`:

```
bin/
obj/
.vs/
.idea/
*.user
TestResults/
.env
```

## Container files

`Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/{Name}.Api/{Name}.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "{Name}.Api.dll"]
```

`docker-compose.yml`. `{database}` is `{Name}` in lower case with dots removed. The same name goes in both `appsettings.json` files and in `PersistenceConstants.DesignTimeConnectionString`.

```yaml
services:
  postgres:
    image: postgres:17
    environment:
      POSTGRES_DB: {database}
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d {database}"]
      interval: 5s
      timeout: 3s
      retries: 10

  api:
    build:
      context: .
      dockerfile: Dockerfile
    environment:
      ConnectionStrings__Default: Host=postgres;Port=5432;Database={database};Username=postgres;Password=postgres
    ports:
      - "8080:8080"
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  postgres-data:
```

## Rules

- `Nullable` is disabled and `ImplicitUsings` enabled, for every project, by `Directory.Build.props`. A project file never repeats them.
- No package without a version line in `Directory.Packages.props`; no version attribute in a project file.
- No package outside this list. A new dependency needs a decision record first.
- The build must end with `0 Warning(s)` and `0 Error(s)`.

