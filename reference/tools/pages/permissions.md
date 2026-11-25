# Permissions, C#

Applies when `auth` is `jwt`. Projects `{Name}.Shared` (folder `Application/`), `{Name}.Application`, `{Name}.Infrastructure` (folder `Authentication/`) and the hosts. Layer 1: `options/auth.md`, `architecture/tenancy.md` ("The caller"). The sign-in this builds on is on `authentication.md`.

**Not built or run yet** (decision 47): hand-written snippets; the standard and advanced variants sign callers in but check no permission yet. It is listed in the repository's `TODO.md` until a build compiles and proves it.

## Ask the developer first

Identity providers carry permissions differently, so the skill does not choose. Before writing this page's code, the scaffold asks the developer:

1. Which claim carries the caller's permissions (`permissions`, `scope`, `roles`, or another name)?
2. In what form: a list claim (a JSON array, one value per permission), one space-separated string (OAuth `scope`), or role names to map to permissions?
3. With roles: which role grants which permissions?

The answers go into `Auth:PermissionsClaim` and `Auth:PermissionsFormat` (`List`, `SpaceSeparated` or `Roles`), and, for roles, into `RolePermissions.cs` in Infrastructure. The snippets below show `List` and `SpaceSeparated`; `Roles` maps each role through that table.

## How it fits together

```
Api request    ContextCommandFilter: CommandContext.Permissions := the permissions claim, read as the developer said
Console run    --permissions a,b,c (the run names its user's permissions with its user; the Console takes no token)
event          carries the permissions of the command that raised it, like its user, tenant and language
               a handler or consumer gives its command ContextOf(@event): the same user, the same permissions
command        CommandPipeline: before validation, the command's permission must be in Context.Permissions -> else 403 forbidden
```

- Every command declares exactly one permission (`IAuthorizedCommand`); there is no allow-list. An architecture test fails for a command that does not.
- There is no system user and no bypass. A command sent by an event handler or consumer runs as the user whose command raised the event and is checked against that user's permissions. In the example, the user who creates an order also needs `products.decrease-stock` (the consumer decreases the stock as that user) and the user who cancels a line `products.increase-stock`. Grant the permissions of the reactions together with the permission that triggers them.
- A failed check is `ForbiddenException` with the permission's name: 403 and the standard error body, before validation, so a caller learns nothing about the shape of a command it may not run.
- Rules that depend on data ("only the owner may cancel") stay in the domain, which already receives the user.
- Permission names are lower case, `{plural}.{verb}` (`products.create`, `orders.complete`), declared as constants next to the commands, one file per aggregate.

## IAuthorizedCommand

File `src/{Name}.Shared/Application/IAuthorizedCommand.cs`:

```csharp
public interface IAuthorizedCommand
{
    static abstract string Permission { get; }
}
```

A command declares it with the aggregate's constant:

```csharp
public record CreateProductCommand : ICommand<ProductDto>, IContextCommand, ITransactionalCommand, IAuthorizedCommand
{
    public static string Permission => ProductPermissions.Create;
```

File `src/{Name}.Application/Products/ProductPermissions.cs`, one per aggregate:

```csharp
public static class ProductPermissions
{
    public const string Create = "products.create";
    public const string Read = "products.read";
    public const string Rename = "products.rename";
    public const string DecreaseStock = "products.decrease-stock";
    public const string IncreaseStock = "products.increase-stock";
}
```

## The context and the events

`CommandContext` gains `public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>();`, `EventBase` gains `public IReadOnlyList<string> Permissions { get; init; }`, and `EventContext` stamps them on every event the command raises, as it stamps the tenant and the language. `CommandContext.FromEvent` copies them back:

```csharp
            Permissions = (@event.Permissions ?? []).ToHashSet()
```

The outbox payload and the RabbitMQ envelope carry them with the rest of the event; a MassTransit message without them has no permissions, so its consumer's commands are refused.

## The check

`CommandPipeline` checks the permission first, before the validators run:

```csharp
    private static void Authorize(TCommand command, CommandContext commandContext)
    {
        var permission = (string)typeof(TCommand).GetProperty(nameof(IAuthorizedCommand.Permission), BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException($"{typeof(TCommand).Name} declares no permission.");

        if (!commandContext.Permissions.Contains(permission))
        {
            throw new ForbiddenException(permission);
        }
    }
```

It is called in `ExecuteAsync` right after `CommandContext.Complete`: `Authorize(command, contextCommand.Context);`. A command without a context (none in the example: every command is a context command with this option) fails the same way, since an empty context has no permissions.

## Reading the claim

`AuthSettings` (`authentication.md`) gains the two settings and the reader:

```csharp
    public string PermissionsClaim { get; set; } = "permissions";
    public PermissionsFormat PermissionsFormat { get; set; } = PermissionsFormat.List;

    public IReadOnlySet<string> PermissionsOf(ClaimsPrincipal principal)
    {
        var values = principal.FindAll(PermissionsClaim).Select(claim => claim.Value);

        return (PermissionsFormat == PermissionsFormat.SpaceSeparated
            ? values.SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            : values).ToHashSet();
    }
```

`PermissionsFormat` is an enum in `Authentication/` with `List`, `SpaceSeparated` and `Roles`. JwtBearer turns a JSON array claim into one claim per value, so `List` reads them all. `ContextCommandFilter` sets `Permissions = authSettings.Value.PermissionsOf(principal)`.

The Console: `ConsoleArguments` (`hosts.md`) accepts `--permissions products.create,products.read`, split on commas into `CommandContext.Permissions`. The development token command accepts `"permissions": ["products.create", ...]` and writes them as a list claim.

## Tests

`test/{Name}.Tests/Application/PermissionRulesTests.cs`, the architecture test:

```csharp
public class PermissionRulesTests
{
    [Fact]
    public void EveryCommand_Should_DeclareAPermission()
    {
        var offenders = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>)))
            .Where(type => !typeof(IAuthorizedCommand).IsAssignableFrom(type))
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }
}
```

`CommandPipelineTests` gains two tests: a context without the permission throws `ForbiddenException` before any validator runs (the validator mock is never called); a context with it handles the command. `AuthSettingsTests` gains one test per format. `scripts/conformance/permissions.sh` proves the answers over HTTP and the Console.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `IAuthorizedCommand.cs` | `{Name}.Shared.Application` | none |
| `ProductPermissions.cs` and the other aggregates' | `{Name}.Application.Products` (and so on) | none |
| `PermissionsFormat.cs` | `{Name}.Infrastructure.Authentication` | none |
| `PermissionRulesTests.cs` | `{Name}.Tests.Application` | `{Name}.Application`, `{Name}.Shared.Application` |

## Decision record

`docs/adr/0019-every-command-declares-a-permission.md`, in `project-docs-adr.md`.
