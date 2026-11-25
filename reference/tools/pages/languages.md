# Languages of messages and data, C#

Applies when `languages.mode` is `messages` or `data`; the sections marked **data** only with `data`. It needs `exceptions` = `coded` or `catalogued` (R5). Layer 1: `options/languages.md`, `architecture/languages.md`.

Two separate things: the messages the system answers with, in the caller's language; and business data (the product's name) stored per language.

## How it fits together

```
the language of a call:
  Api      Languages.Resolve([the token's language claim, then Accept-Language by quality])
  Console  Languages.Resolve([the token's language claim]); no token: Languages.Default
  -> CommandContext.Language; every event carries it, so a consumer's command runs in the same language
messages:
  an exception has a code and parameters -> at the edge (ExceptionHandler, the Console's error line)
  MessageLocalizer.Get(code, parameters, language) reads messages.<language>.json, falls back to the default language,
  then to the exception's own English message
data:
  Product.Name is a LocalizedText: { "en": "Bolt", "tr": "Cıvata" } in one jsonb column
  a write stores one text under the caller's language; a read returns the caller's language, falling back to the default,
  plus every translation in `names`
```

- The default and the supported languages are generated constants (`Languages`), from `languages.default` and `languages.supported` of `MORTIS.md`, never settings.
- A claim comes before `Accept-Language`, so a user's profile language wins over the browser's. An unsupported language falls back to the default; `tr-TR` matches `tr`.
- The exception's own message stays English and is what the log line shows; only the answer to the caller is translated.
- Field messages of `validation_failed` are the validators' own and are not translated.
- A query whose answer depends on the language (`GetProductCommand`) carries the context like a command, so the host can fill its language.

## Languages

File `src/{Name}.Shared/Application/Languages.cs`, written from `MORTIS.md`:

@@decl src/Boilerplate.Shared/Application/Languages.cs::Languages@@

@@members src/Boilerplate.Shared/Application/Languages.cs::Languages@@

## Exceptions with parameters

`ExceptionBase` (`shared-other.md`) gains the parameters its message is made of; each failure passes them by name:

@@decl variant:advanced/src/Boilerplate.Shared/Exceptions/ExceptionBase.cs::ExceptionBase@@

@@members variant:advanced/src/Boilerplate.Shared/Exceptions/ExceptionBase.cs::ExceptionBase@@

@@decl variant:advanced/src/Boilerplate.Shared/Exceptions/NotFoundException.cs::NotFoundException@@

@@decl variant:advanced/src/Boilerplate.Shared/Exceptions/InvalidParameterException.cs::InvalidParameterException@@

The others the same way: `DuplicateException` `{name}`, `ForbiddenException` `{action}`, `ReferenceViolationException` `{name}` (the constraint), `TransactionRequiredException` `{name}`, `TenantRequiredException` `{type}`. `BusinessRuleException` and `ValidationFailedException` have none: their texts are fixed per code.

With `exceptions` = `catalogued`, `ErrorCatalogue` keeps only the codes; the texts move to the message files:

@@members variant:advanced/src/Boilerplate.Domain/ErrorCatalogue.cs::ErrorCatalogue@@

## The message files and MessageLocalizer

Folder `src/{Name}.Infrastructure/Localization/`, one file per supported language, keyed by code, `{parameter}` where a parameter goes:

@@data src/Boilerplate.Infrastructure/Localization/messages.en.json json@@

@@data src/Boilerplate.Infrastructure/Localization/messages.tr.json json@@

They are embedded resources. `WithCulture="false"` matters: without it MSBuild reads `.en` and `.tr` as cultures and moves the files into satellite assemblies:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Localization/messages.*.json" WithCulture="false" />
  </ItemGroup>
```

File `MessageLocalizer.cs`, a singleton registered by `AddInfrastructureModule` (`services.AddSingleton<MessageLocalizer>();`). The folder is `Localization`, not `Languages`, so its namespace does not hide the `Languages` class:

@@decl src/Boilerplate.Infrastructure/Localization/MessageLocalizer.cs::MessageLocalizer@@

@@members src/Boilerplate.Infrastructure/Localization/MessageLocalizer.cs::MessageLocalizer@@

## The hosts

File `src/{Name}.Api/Middlewares/RequestLanguage.cs`:

@@decl src/Boilerplate.Api/Middlewares/RequestLanguage.cs::RequestLanguage@@

@@members src/Boilerplate.Api/Middlewares/RequestLanguage.cs::RequestLanguage@@

`AuthSettings` (`authentication.md`) gains `LanguageClaim` (default `locale`) and `LanguageOf(principal)`; `ContextCommandFilter` sets `Language = RequestLanguage.Of(context.HttpContext, authSettings.Value)`.

`ExceptionHandler` takes `MessageLocalizer` and `IOptions<AuthSettings>` and answers in the request's language:

@@member variant:advanced/src/Boilerplate.Api/Middlewares/ExceptionHandler.cs::ExceptionHandler::TryHandleAsync@@

The Console: `ConsoleTokens` sets `Language` from the token (`Languages.Default` without one), the development token accepts `"language"`, and `Program.cs` translates its error line with the run's language (`authentication.md` has the file).

`LogFields` writes `Language` on every command and event line (`logging.md`), so the language of a consumer's command can be seen.

## Data: LocalizedText

**data.** File `src/{Name}.Shared/Domain/LocalizedText.cs`, a value object:

@@decl src/Boilerplate.Shared/Domain/LocalizedText.cs::LocalizedText@@

@@members src/Boilerplate.Shared/Domain/LocalizedText.cs::LocalizedText@@

Stored as one `jsonb` column through a converter, file `src/{Name}.Infrastructure/Persistence/LocalizedTextConversion.cs`:

@@decl src/Boilerplate.Infrastructure/Persistence/LocalizedTextConversion.cs::LocalizedTextConversion@@

@@members src/Boilerplate.Infrastructure/Persistence/LocalizedTextConversion.cs::LocalizedTextConversion@@

```csharp
builder.Property(product => product.Name).HasConversion(LocalizedTextConversion.Converter, LocalizedTextConversion.Comparer).HasColumnType("jsonb").IsRequired();
```

Searching or sorting by a translated field uses the language's JSON path (`name ->> 'tr'`); an expression index per supported language is the way to make that fast, written by hand when a query needs it.

## Data: the example

**data**, with `example` = `true`. `Product.Name` is a `LocalizedText`; the constructor takes one, and a new operation renames in one language:

@@member variant:advanced/src/Boilerplate.Domain/Aggregates/Products/Product.cs::Product::Rename@@

`CreateProductCommand` still takes one `Name`; the handler stores it under the caller's language:

@@member variant:advanced/src/Boilerplate.Application/Products/CreateProductCommand.cs::CreateProductCommandHandler::OnHandle@@

`ProductDto` returns the caller's language and every translation; `Map` takes the language, and every handler passes `command.Context.Language`:

@@decl variant:advanced/src/Boilerplate.Application/Products/ProductDto.cs::ProductDto@@

@@members variant:advanced/src/Boilerplate.Application/Products/ProductDto.cs::ProductDto@@

With `queries` = `commands`, `GetProductCommand` carries the context, and the controller binds it from the route so the filter fills it (with `split` or `read-models`, `GetProductQuery` does the same, `queries.md`):

```csharp
public record GetProductCommand : ICommand<ProductDto>, IContextCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; }
}
```

Its handler returns `ProductDto.Map(product, command.Context?.Language)`.

```csharp
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] GetProductCommand command)
```

File `src/{Name}.Application/Products/RenameProductCommand.cs`, registered in `ApplicationModule` after `CreateProductCommand`, and `PUT /products/{id}/name`:

@@decl src/Boilerplate.Application/Products/RenameProductCommand.cs::RenameProductCommand@@

@@member src/Boilerplate.Application/Products/RenameProductCommand.cs::RenameProductCommandValidator::RenameProductCommandValidator@@

@@member src/Boilerplate.Application/Products/RenameProductCommand.cs::RenameProductCommandHandler::OnHandle@@

@@member variant:advanced/src/Boilerplate.Api/Controllers/ProductsController.cs::ProductsController::Rename@@

## Tests

`test/{Name}.Tests/Shared/Application/LanguagesTests.cs`:

@@members test/Boilerplate.Tests/Shared/Application/LanguagesTests.cs::LanguagesTests@@

`test/{Name}.Tests/Infrastructure/Localization/MessageLocalizerTests.cs`. The first two fail when a catalogued code has no text, in the default language or in any other:

@@members test/Boilerplate.Tests/Infrastructure/Localization/MessageLocalizerTests.cs::MessageLocalizerTests@@

`ExceptionHandlerTests` gains the language theory:

@@member variant:advanced/test/Boilerplate.Tests/Api/Middlewares/ExceptionHandlerTests.cs::ExceptionHandlerTests::TryHandleAsync_Should_WriteTheMessageInTheRequestedLanguageOrTheDefault@@

**data:** `test/{Name}.Tests/Shared/Domain/LocalizedTextTests.cs` and `RenameProductCommandHandlerTests`:

@@members test/Boilerplate.Tests/Shared/Domain/LocalizedTextTests.cs::LocalizedTextTests@@

@@members test/Boilerplate.Tests/Application/Products/RenameProductCommandHandlerTests.cs::RenameProductCommandHandlerTests@@

`ProductTests` gains `Rename_Should_AddOrReplaceTheNameInThatLanguageOnly`; `FakeProduct`, `ProductTests` and the persistence tests build names with `LocalizedText.Of`; `ProductRepositoryBuilder` gains `WithFindByIdTrackingAsync`. `scripts/conformance/languages.sh` proves the answers, the stored names and the consumer's language against the running hosts.

## Decision record

`docs/adr/0016-languages-of-messages-and-data.md`, in `project-docs-adr.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings languages.md@@
