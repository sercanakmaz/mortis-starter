# Failures: the three levels, C#

`MORTIS.md` `exceptions` picks one level. Layer 1: `building-blocks.md` "Failure". The error body and the status of every failure are **the same at every level**, so a project moves up a level without breaking a client:

| Failure | Status | Code |
|---|---|---|
| not found | 404 | `not_found` |
| validation | 422 | `validation_failed`, with `errors` naming the fields |
| business rule | 409 | the rule's own code |
| duplicate (a unique index) | 409 at `coded` and `catalogued`; 500 at `minimal` | `duplicate`; `unexpected_error` at `minimal` |
| reference (a foreign key) | 400 at `coded` and `catalogued`; 500 at `minimal` | `reference_violation`; `unexpected_error` at `minimal` |
| anything else | 500 | `unexpected_error`, with a reference in the message |

Body: `{ "code": "...", "message": "...", "errors": { "<field>": ["..."] } }`; `errors` only for `validation_failed`.

## What each level adds

| Level | Adds | Files |
|---|---|---|
| `minimal` | the failure family of `shared-other.md`; a database error is an unexpected failure | no `DatabaseExceptionHandler`; `UnitOfWork.SaveChangesAsync` has no `catch` (below) |
| `coded` | database errors translated: unique → `DuplicateException`, foreign key → `ReferenceViolationException`, check and not-null → `InvalidParameterException` | `Persistence/DatabaseExceptionHandler.cs` (`infrastructure-postgres.md`) |
| `catalogued` | every code declared once, with its default message, in `ErrorCatalogue`; a test fails when a rule code is missing from it; translated messages (the `languages` option) read from it | `src/{Name}.Domain/ErrorCatalogue.cs`, its test |

At every level: the log line of a failure is a warning for 4xx and an error for 5xx; an unexpected failure answers with a short reference (`ErrorReference`) that is also on its log line, never with its own message; the Console's exit codes are 1 for an unknown name and 2 for a failure.

A rule code is always a constant of the aggregate's `{Aggregate}Constants`, never a string written at the throw; `verify.sh` fails `new BusinessRuleException("...")`.

## minimal: SaveChangesAsync

```csharp
public Task<int> SaveChangesAsync()
{
    return context.SaveChangesAsync();
}
```

At `coded` and `catalogued` it is the one in `infrastructure-postgres.md`, with the `catch` that calls `DatabaseExceptionHandler.Handle`.

## The reference of an unexpected failure

File `src/{Name}.Api/Middlewares/ErrorReference.cs`, at every level. The same failure at the same place always gets the same reference, so they group in the logs.

```csharp
public static class ErrorReference
```

```csharp
public static string Of(Exception exception)
{
    var place = $"{exception.GetType().FullName}|{exception.TargetSite?.DeclaringType?.FullName}.{exception.TargetSite?.Name}";

    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(place)))[..8].ToLowerInvariant();
}
```

The exception handler that uses it is in `hosts.md`.

## catalogued: the catalogue

File `src/{Name}.Domain/ErrorCatalogue.cs`, namespace `{Name}.Domain`. The seven shared codes, then one line per rule code of every aggregate, by its constant. The example's lines are written only with the example.

```csharp
public static class ErrorCatalogue
```

```csharp
public static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<string, string>
{
    ["not_found"] = "The record was not found.",
    ["duplicate"] = "The record already exists.",
    ["forbidden"] = "The action is not allowed.",
    ["invalid_parameter"] = "A value is not valid.",
    ["reference_violation"] = "A reference points at a missing record, or the record is still referenced.",
    ["transaction_required"] = "A write ran outside a transaction.",
    ["validation_failed"] = "The request is not valid.",
    [CustomerConstants.BlockedCode] = "A blocked customer cannot complete orders.",
    [ProductConstants.InsufficientStockCode] = "The product does not have enough stock.",
    [OrderConstants.NotOpenCode] = "The order is not open.",
    [OrderConstants.MixedCurrencyCode] = "Every line of an order uses the same currency.",
    [OrderConstants.NothingToFulfilCode] = "An order without an active line cannot be fulfilled.",
    [OrderConstants.LineAlreadyCancelledCode] = "The order line is already cancelled."
};
```

Test, `test/{Name}.Tests/Domain/ErrorCatalogueTests.cs`:

```csharp
public class ErrorCatalogueTests
```

```csharp
[Fact]
public void Messages_Should_DeclareEveryRuleCodeOfTheDomain()
{
    var codes = typeof(ErrorCatalogue).Assembly.GetTypes()
        .Where(type => type.Name.EndsWith("Constants"))
        .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(field => field.IsLiteral && field.Name.EndsWith("Code"))
        .Select(field => (string)field.GetRawConstantValue())
        .ToList();

    Assert.All(codes, code => Assert.True(ErrorCatalogue.Messages.ContainsKey(code), $"{code} has no message"));
}
```

```csharp
[Fact]
public void Messages_Should_DeclareTheCodesOfTheSharedFailures()
{
    Assert.All(["not_found", "duplicate", "forbidden", "invalid_parameter", "reference_violation", "transaction_required", "validation_failed"], code => Assert.True(ErrorCatalogue.Messages.ContainsKey(code), code));
}
```

## Decision record

`docs/adr/0010-one-error-body.md`, in `project-docs-adr.md`; its first line of "Decision" names the level of this project.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Api/Middlewares/ErrorReference.cs` | `{Name}.Api.Middlewares` | System.Security.Cryptography, System.Text |
| `src/{Name}.Domain/ErrorCatalogue.cs` | `{Name}.Domain` | {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Products |
| `test/{Name}.Tests/Domain/ErrorCatalogueTests.cs` | `{Name}.Tests.Domain` | System.Reflection, {Name}.Domain |
