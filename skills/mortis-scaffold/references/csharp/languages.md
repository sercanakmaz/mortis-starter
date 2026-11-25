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

```csharp
public static class Languages
```


```csharp
public const string Default = "en";
```

```csharp
public static readonly IReadOnlyList<string> Supported = ["en", "tr"];
```

```csharp
public static string Resolve(IEnumerable<string> candidates)
{
    foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
    {
        var language = Supported.FirstOrDefault(supported => string.Equals(supported, candidate, StringComparison.OrdinalIgnoreCase))
            ?? Supported.FirstOrDefault(supported => string.Equals(supported, candidate.Split('-')[0], StringComparison.OrdinalIgnoreCase));

        if (language is not null) return language;
    }

    return Default;
}
```

## Exceptions with parameters

`ExceptionBase` (`shared-other.md`) gains the parameters its message is made of; each failure passes them by name:

```csharp
public abstract class ExceptionBase(string code, HttpStatusCode httpStatusCode, string message, Exception innerException = null, IReadOnlyDictionary<string, object> parameters = null) : Exception(message, innerException)
```


```csharp
public IReadOnlyDictionary<string, object> Parameters { get; } = parameters ?? new Dictionary<string, object>();
```

```csharp
public string Code { get; } = code;
```

```csharp
public HttpStatusCode HttpStatusCode { get; } = httpStatusCode;
```

```csharp
public class NotFoundException(string name) : ExceptionBase("not_found", HttpStatusCode.NotFound, $"{name} was not found.", parameters: new Dictionary<string, object> { ["name"] = name });
```

```csharp
public class InvalidParameterException(string name, string reason, Exception innerException = null) : ExceptionBase("invalid_parameter", HttpStatusCode.BadRequest, $"{name} is invalid: {reason}", innerException, new Dictionary<string, object> { ["name"] = name, ["reason"] = reason });
```

The others the same way: `DuplicateException` `{name}`, `ForbiddenException` `{action}`, `ReferenceViolationException` `{name}` (the constraint), `TransactionRequiredException` `{name}`, `TenantRequiredException` `{type}`. `BusinessRuleException` and `ValidationFailedException` have none: their texts are fixed per code.

With `exceptions` = `catalogued`, `ErrorCatalogue` keeps only the codes; the texts move to the message files:

```csharp
public static class ErrorCatalogue
```

```csharp
public static readonly IReadOnlySet<string> Codes = new HashSet<string>
{
    "not_found",
    "duplicate",
    "forbidden",
    "invalid_parameter",
    "reference_violation",
    "transaction_required",
    "tenant_required",
    "validation_failed",
    "idempotency_key_reused",
    "idempotency_in_progress",
    LoopGuardConstants.LoopDetectedCode,
    CustomerConstants.BlockedCode,
    ProductConstants.InsufficientStockCode,
    OrderConstants.NotOpenCode,
    OrderConstants.MixedCurrencyCode,
    OrderConstants.NothingToFulfilCode,
    OrderConstants.LineAlreadyCancelledCode
};
```

## The message files and MessageLocalizer

Folder `src/{Name}.Infrastructure/Localization/`, one file per supported language, keyed by code, `{parameter}` where a parameter goes:

```json
{
  "not_found": "{name} was not found.",
  "duplicate": "{name} already exists.",
  "forbidden": "{action} is not allowed.",
  "invalid_parameter": "{name} is invalid: {reason}",
  "reference_violation": "The reference {name} points at a missing record, or the record is still referenced.",
  "idempotency_key_reused": "This idempotency key was used for another request.",
  "idempotency_in_progress": "A request with this idempotency key is still running.",
  "loop_detected": "Commands triggered each other too many times: {chain}.",
  "transaction_required": "A write to {name} ran outside a transaction.",
  "tenant_required": "{type} belongs to a tenant, and the caller has none.",
  "validation_failed": "The request is not valid.",
  "customer_blocked": "A blocked customer cannot complete orders.",
  "product_insufficient_stock": "The product does not have enough stock.",
  "order_not_open": "The order is not open.",
  "order_mixed_currency": "Every line of an order uses the same currency.",
  "order_nothing_to_fulfil": "An order without an active line cannot be fulfilled.",
  "order_line_already_cancelled": "The order line is already cancelled."
}
```

```json
{
  "not_found": "{name} bulunamadı.",
  "duplicate": "{name} zaten var.",
  "forbidden": "{action} işlemine izin verilmiyor.",
  "invalid_parameter": "{name} geçersiz: {reason}",
  "reference_violation": "{name} başvurusu olmayan bir kaydı gösteriyor ya da kayıt hâlâ kullanılıyor.",
  "idempotency_key_reused": "Bu idempotency anahtarı başka bir istek için kullanıldı.",
  "idempotency_in_progress": "Bu idempotency anahtarıyla gelen bir istek hâlâ çalışıyor.",
  "loop_detected": "Komutlar birbirini çok fazla kez tetikledi: {chain}.",
  "transaction_required": "{name} kaydına bir işlem (transaction) dışında yazıldı.",
  "tenant_required": "{type} bir kiracıya ait, çağıranın ise kiracısı yok.",
  "validation_failed": "İstek geçerli değil.",
  "customer_blocked": "Engellenmiş bir müşteri sipariş tamamlayamaz.",
  "product_insufficient_stock": "Ürünün stoğu yeterli değil.",
  "order_not_open": "Sipariş açık değil.",
  "order_mixed_currency": "Bir siparişin tüm satırları aynı para birimini kullanır.",
  "order_nothing_to_fulfil": "Etkin satırı olmayan bir sipariş tamamlanamaz.",
  "order_line_already_cancelled": "Sipariş satırı zaten iptal edilmiş."
}
```

They are embedded resources. `WithCulture="false"` matters: without it MSBuild reads `.en` and `.tr` as cultures and moves the files into satellite assemblies:

```xml
  <ItemGroup>
    <EmbeddedResource Include="Localization/messages.*.json" WithCulture="false" />
  </ItemGroup>
```

File `MessageLocalizer.cs`, a singleton registered by `AddInfrastructureModule` (`services.AddSingleton<MessageLocalizer>();`). The folder is `Localization`, not `Languages`, so its namespace does not hide the `Languages` class:

```csharp
public class MessageLocalizer
```


```csharp
private readonly Dictionary<string, Dictionary<string, string>> _messages = Languages.Supported.ToDictionary(language => language, Load);
```

```csharp
public IReadOnlyDictionary<string, string> MessagesOf(string language)
{
    return _messages[language];
}
```

```csharp
public string Get(string code, IReadOnlyDictionary<string, object> parameters, string language)
{
    var template = Find(language, code) ?? Find(Languages.Default, code);

    return template is null ? null : parameters.Aggregate(template, (text, parameter) => text.Replace("{" + parameter.Key + "}", Convert.ToString(parameter.Value, CultureInfo.InvariantCulture)));
}
```

```csharp
private string Find(string language, string code)
{
    return language is not null && _messages.TryGetValue(language, out var messages) && messages.TryGetValue(code, out var template) ? template : null;
}
```

```csharp
private static Dictionary<string, string> Load(string language)
{
    using var stream = typeof(MessageLocalizer).Assembly.GetManifestResourceStream($"{typeof(MessageLocalizer).Namespace}.messages.{language}.json");

    return stream is null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
}
```

## The hosts

File `src/{Name}.Api/Middlewares/RequestLanguage.cs`:

```csharp
public static class RequestLanguage
```


```csharp
public static string Of(HttpContext httpContext, AuthSettings authSettings)
{
    var acceptedLanguages = httpContext.Request.GetTypedHeaders().AcceptLanguage
        .OrderByDescending(language => language.Quality ?? 1)
        .Select(language => language.Value.Value);

    return Languages.Resolve(new[] { authSettings.LanguageOf(httpContext.User) }.Concat(acceptedLanguages));
}
```

`AuthSettings` (`authentication.md`) gains `LanguageClaim` (default `locale`) and `LanguageOf(principal)`; `ContextCommandFilter` sets `Language = RequestLanguage.Of(context.HttpContext, authSettings.Value)`.

`ExceptionHandler` takes `MessageLocalizer` and `IOptions<AuthSettings>` and answers in the request's language:

```csharp
public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
{
    var exceptionBase = exception as ExceptionBase;
    var httpStatusCode = exceptionBase?.HttpStatusCode ?? HttpStatusCode.InternalServerError;
    var code = exceptionBase?.Code ?? ApiConstants.UnexpectedErrorCode;
    var reference = ErrorReference.Of(exception);
    var isServerError = httpStatusCode >= HttpStatusCode.InternalServerError;

    logger.Log(isServerError ? LogLevel.Error : LogLevel.Warning, isServerError ? exception : null, "Request failed with {StatusCode} {Code} reference {Reference}", (int)httpStatusCode, code, reference);

    httpContext.Response.StatusCode = (int)httpStatusCode;

    await httpContext.Response.WriteAsJsonAsync(new ErrorResponseModel(
        code,
        exceptionBase is null ? $"{ApiConstants.UnexpectedErrorMessage} Reference {reference}." : messageLocalizer.Get(code, exceptionBase.Parameters, RequestLanguage.Of(httpContext, authSettings.Value)) ?? exceptionBase.Message,
        (exception as ValidationFailedException)?.Errors), cancellationToken);

    return true;
}
```

The Console: `ConsoleTokens` sets `Language` from the token (`Languages.Default` without one), the development token accepts `"language"`, and `Program.cs` translates its error line with the run's language (`authentication.md` has the file).

`LogFields` writes `Language` on every command and event line (`logging.md`), so the language of a consumer's command can be seen.

## Data: LocalizedText

**data.** File `src/{Name}.Shared/Domain/LocalizedText.cs`, a value object:

```csharp
public sealed class LocalizedText
```


```csharp
private readonly Dictionary<string, string> _texts;
```

```csharp
private LocalizedText(Dictionary<string, string> texts)
{
    _texts = texts;
}
```

```csharp
public IReadOnlyDictionary<string, string> Texts => _texts;
```

```csharp
public static LocalizedText Of(string language, string text)
{
    return new LocalizedText(new Dictionary<string, string> { [language] = text });
}
```

```csharp
public static LocalizedText FromTexts(IReadOnlyDictionary<string, string> texts)
{
    return new LocalizedText(new Dictionary<string, string>(texts));
}
```

```csharp
public LocalizedText With(string language, string text)
{
    return new LocalizedText(new Dictionary<string, string>(_texts) { [language] = text });
}
```

```csharp
public string Get(string language, string fallbackLanguage)
{
    if (language is not null && _texts.TryGetValue(language, out var text)) return text;

    return _texts.TryGetValue(fallbackLanguage, out var fallback) ? fallback : _texts.Values.FirstOrDefault();
}
```

Stored as one `jsonb` column through a converter, file `src/{Name}.Infrastructure/Persistence/LocalizedTextConversion.cs`:

```csharp
public static class LocalizedTextConversion
```


```csharp
public static readonly ValueConverter<LocalizedText, string> Converter = new(text => Write(text), json => Read(json));
```

```csharp
public static readonly ValueComparer<LocalizedText> Comparer = new(
    (left, right) => Write(left) == Write(right),
    text => Write(text).GetHashCode(),
    text => Read(Write(text)));
```

```csharp
public static string Write(LocalizedText text)
{
    return text is null ? null : JsonSerializer.Serialize(new SortedDictionary<string, string>(text.Texts.ToDictionary()));
}
```

```csharp
public static LocalizedText Read(string json)
{
    return json is null ? null : LocalizedText.FromTexts(JsonSerializer.Deserialize<Dictionary<string, string>>(json));
}
```

```csharp
builder.Property(product => product.Name).HasConversion(LocalizedTextConversion.Converter, LocalizedTextConversion.Comparer).HasColumnType("jsonb").IsRequired();
```

Searching or sorting by a translated field uses the language's JSON path (`name ->> 'tr'`); an expression index per supported language is the way to make that fast, written by hand when a query needs it.

## Data: the example

**data**, with `example` = `true`. `Product.Name` is a `LocalizedText`; the constructor takes one, and a new operation renames in one language:

```csharp
public void Rename(string language, string name)
{
    Name = Name.With(language, name);

    RaiseEvent(new Renamed(Id.ToString(), Sku));
}
```

`CreateProductCommand` still takes one `Name`; the handler stores it under the caller's language:

```csharp
protected override async Task<CommandResult<ProductDto>> OnHandle(CreateProductCommand command)
{
    var product = new Product(command.Id, command.Sku, LocalizedText.Of(command.Context.Language ?? Languages.Default, command.Name), command.Stock, command.Context.User, command.CategoryId);

    await productRepository.CreateAsync(product);

    return CommandResult.FromContent(ProductDto.Map(product, command.Context.Language));
}
```

`ProductDto` returns the caller's language and every translation; `Map` takes the language, and every handler passes `command.Context.Language`:

```csharp
public record ProductDto
```


```csharp
public Guid Id { get; set; }
```

```csharp
public string Sku { get; set; }
```

```csharp
public string Name { get; set; }
```

```csharp
public IReadOnlyDictionary<string, string> Names { get; set; }
```

```csharp
public int Stock { get; set; }
```

```csharp
public int? CategoryId { get; set; }
```

```csharp
public static ProductDto Map(Product product, string language) => new()
{
    Id = product.Id,
    Sku = product.Sku,
    Name = product.Name.Get(language, Languages.Default),
    Names = product.Name.Texts,
    Stock = product.Stock,
    CategoryId = product.CategoryId
};
```

```csharp
public static ProductDto From(ProductView view, string language) => new()
{
    Id = view.Id,
    Sku = view.Sku,
    Name = view.Name.Get(language, Languages.Default),
    Names = view.Name.Texts,
    Stock = view.Stock,
    CategoryId = view.CategoryId
};
```

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

```csharp
public record RenameProductCommand : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
```

```csharp
public RenameProductCommandValidator()
{
    RuleFor(command => command.Id).NotEmpty();
    RuleFor(command => command.Name).NotEmpty().MaximumLength(ProductConstants.NameMaxLength);
}
```

```csharp
protected override async Task<CommandResult<ProductDto>> OnHandle(RenameProductCommand command)
{
    var product = await productRepository.FindByIdTrackingAsync(command.Id)
        ?? throw new NotFoundException(nameof(Product));

    product.Rename(command.Context.Language ?? Languages.Default, command.Name);
    product.SetUpdatedAudit(command.Context.User);

    await productRepository.UpdateAsync(product);

    return CommandResult.FromContent(ProductDto.Map(product, command.Context.Language));
}
```

```csharp
[HttpPut("{id:guid}/name")]
public async Task<IActionResult> Rename([FromRoute] Guid id, [FromBody] RenameProductCommand command)
{
    var commandResult = await commandDispatcher.Send(command with { Id = id });

    return Ok(commandResult.Content);
}
```

## Tests

`test/{Name}.Tests/Shared/Application/LanguagesTests.cs`:

```csharp
public class LanguagesTests
```

```csharp
[Theory]
[InlineData("tr", null, "tr")]
[InlineData("TR-tr", null, "tr")]
[InlineData("de", "tr", "tr")]
[InlineData("de", null, "en")]
[InlineData(null, "", "en")]
public void Resolve_Should_TakeTheFirstSupportedCandidateOrItsPrimaryTagElseTheDefault(string first, string second, string language)
{
    Assert.Equal(language, Languages.Resolve([first, second]));
}
```

`test/{Name}.Tests/Infrastructure/Localization/MessageLocalizerTests.cs`. The first two fail when a catalogued code has no text, in the default language or in any other:

```csharp
public class MessageLocalizerTests
```

```csharp
[Fact]
public void MessagesOf_TheDefaultLanguage_Should_HaveATextForEveryCataloguedCode()
{
    var messages = new MessageLocalizer().MessagesOf(Languages.Default);

    Assert.DoesNotContain(ErrorCatalogue.Codes, code => !messages.ContainsKey(code));
}
```

```csharp
[Fact]
public void MessagesOf_EverySupportedLanguage_Should_HaveATextForEveryCataloguedCode()
{
    var messageLocalizer = new MessageLocalizer();

    var missing = Languages.Supported.SelectMany(language => ErrorCatalogue.Codes.Where(code => !messageLocalizer.MessagesOf(language).ContainsKey(code)).Select(code => $"{language}: {code}"));

    Assert.Empty(missing);
}
```

```csharp
[Fact]
public void Get_Should_FillTheParametersIntoTheTextOfTheLanguage()
{
    var message = new MessageLocalizer().Get("invalid_parameter", new Dictionary<string, object> { ["name"] = "stock", ["reason"] = "eksi olamaz" }, "tr");

    Assert.Equal("stock geçersiz: eksi olamaz", message);
}
```

```csharp
[Fact]
public void Get_WithAnUnsupportedLanguage_Should_FallBackToTheDefault_And_WithAnUnknownCode_Should_ReturnNull()
{
    var messageLocalizer = new MessageLocalizer();

    Assert.Equal("The order is not open.", messageLocalizer.Get("order_not_open", new Dictionary<string, object>(), "de"));
    Assert.Null(messageLocalizer.Get("no_such_code", new Dictionary<string, object>(), "en"));
}
```

`ExceptionHandlerTests` gains the language theory:

```csharp
[Theory]
[InlineData("tr", "Product bulunamadı.")]
[InlineData("tr-TR,en;q=0.5", "Product bulunamadı.")]
[InlineData("de", "Product was not found.")]
public async Task TryHandleAsync_Should_WriteTheMessageInTheRequestedLanguageOrTheDefault(string acceptLanguage, string message)
{
    var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
    httpContext.Request.Headers.AcceptLanguage = acceptLanguage;

    await new ExceptionHandler(new RecordingLogger<ExceptionHandler>(), new MessageLocalizer(), Options.Create(new AuthSettings())).TryHandleAsync(httpContext, new NotFoundException("Product"), CancellationToken.None);

    httpContext.Response.Body.Position = 0;
    var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
    Assert.Equal(message, body.GetProperty("message").GetString());
}
```

**data:** `test/{Name}.Tests/Shared/Domain/LocalizedTextTests.cs` and `RenameProductCommandHandlerTests`:

```csharp
public class LocalizedTextTests
```

```csharp
[Fact]
public void Get_Should_ReturnTheTextOfTheLanguage_ThenTheFallback_ThenAnyText()
{
    var localizedText = LocalizedText.Of("tr", "Cıvata").With("en", "Bolt");

    Assert.Equal(["Cıvata", "Bolt", "Bolt"], [localizedText.Get("tr", "en"), localizedText.Get("en", "en"), localizedText.Get("de", "en")]);
    Assert.Equal("Cıvata", LocalizedText.Of("tr", "Cıvata").Get("de", "en"));
}
```

```csharp
[Fact]
public void With_Should_LeaveTheOriginalUnchanged()
{
    var original = LocalizedText.Of("tr", "Cıvata");

    original.With("en", "Bolt");

    Assert.Equal(["tr"], original.Texts.Keys);
}
```

```csharp
public class RenameProductCommandHandlerTests
```

```csharp
[Fact]
public async Task Handle_Should_StoreTheNameUnderTheCallersLanguageAndReturnItInThatLanguage()
{
    var product = FakeProduct.Create("SKU-1", 10);
    var commandPipeline = CommandPipelineBuilder<RenameProductCommand>.Create().WithValidator(new RenameProductCommandValidator()).Build();
    var commandHandler = new RenameProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(product).WithUpdateAsync().Build(), commandPipeline);

    var commandResult = await commandHandler.Handle(new RenameProductCommand { Id = product.Id, Name = "Cıvata", Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost"), Language = "tr" } });

    Assert.Equal("Cıvata", commandResult.Content.Name);
    Assert.Equal(["Default Product", "Cıvata"], [commandResult.Content.Names[Languages.Default], commandResult.Content.Names["tr"]]);
    Assert.Equal("user-1", product.UpdatedUserId);
}
```

```csharp
[Fact]
public async Task Handle_WithAnUnknownProduct_Should_ThrowNotFound()
{
    var commandPipeline = CommandPipelineBuilder<RenameProductCommand>.Create().WithValidator(new RenameProductCommandValidator()).Build();
    var commandHandler = new RenameProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).Build(), commandPipeline);

    await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new RenameProductCommand { Id = Guid.CreateVersion7(), Name = "Bolt" }));
}
```

`ProductTests` gains `Rename_Should_AddOrReplaceTheNameInThatLanguageOnly`; `FakeProduct`, `ProductTests` and the persistence tests build names with `LocalizedText.Of`; `ProductRepositoryBuilder` gains `WithFindByIdTrackingAsync`. `scripts/conformance/languages.sh` proves the answers, the stored names and the consumer's language against the running hosts.

## Decision record

`docs/adr/0016-languages-of-messages-and-data.md`, in `project-docs-adr.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Api/Middlewares/RequestLanguage.cs` | `{Name}.Api.Middlewares` | {Name}.Infrastructure.Authentication, {Name}.Shared.Application |
| `src/{Name}.Application/Products/RenameProductCommand.cs` | `{Name}.Application.Products` | FluentValidation, System.Text.Json.Serialization, {Name}.Domain.Aggregates.Products, {Name}.Shared.Application, {Name}.Shared.Exceptions |
| `src/{Name}.Infrastructure/Localization/MessageLocalizer.cs` | `{Name}.Infrastructure.Localization` | System.Globalization, System.Text.Json, {Name}.Shared.Application |
| `src/{Name}.Infrastructure/Persistence/LocalizedTextConversion.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore.ChangeTracking, Microsoft.EntityFrameworkCore.Storage.ValueConversion, System.Text.Json, {Name}.Shared.Domain |
| `src/{Name}.Shared/Application/Languages.cs` | `{Name}.Shared.Application` | none |
| `src/{Name}.Shared/Domain/LocalizedText.cs` | `{Name}.Shared.Domain` | none |
| `test/{Name}.Tests/Application/Products/RenameProductCommandHandlerTests.cs` | `{Name}.Tests.Application.Products` | {Name}.Application.Products, {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Domain.Products, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Infrastructure/Localization/MessageLocalizerTests.cs` | `{Name}.Tests.Infrastructure.Localization` | {Name}.Domain, {Name}.Infrastructure.Localization, {Name}.Shared.Application |
| `test/{Name}.Tests/Shared/Application/LanguagesTests.cs` | `{Name}.Tests.Shared.Application` | {Name}.Shared.Application |
| `test/{Name}.Tests/Shared/Domain/LocalizedTextTests.cs` | `{Name}.Tests.Shared.Domain` | {Name}.Shared.Domain |
