# Secrets at rest, C#

Applies when `addons` contains `secrets` and `database` is `postgres` (with MongoDB not offered yet: R14, a note). Projects `{Name}.Shared` (folder `Application/`), `{Name}.Infrastructure` (folder `Secrets/`, and the configurations that use it) and, with the example, the customer slice. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Built** in the advanced reference solution (decision 52): compiled, tested and verified there.

## How it fits together

```
ISecretProtector (port)   Protect(plaintext) -> envelope text;  Unprotect(envelope) -> plaintext
envelope                  v1.{keyId}.{wrappedDataKey}.{nonce}.{ciphertext}.{tag}   (base64url parts)
  data key                a fresh 32-byte AES-256-GCM key per value
  key-encryption key      Secrets:Keys:{keyId} (32 bytes, base64), the current one Secrets:CurrentKeyId; it wraps the data key (AES-GCM)
persistence               builder.Property(x => x.TaxNumber).IsSecret() marks the column; AppDbContext puts a converter around the protector on every marked column
domain                    sees plaintext; the database and its backups see only envelopes
rotation                  add a key, make it current: new writes use it, old envelopes still open with the key they name;
                          a value is re-encrypted with the current key the next time it is written
```

- The protector is in Infrastructure; the domain never knows a value is encrypted. A column marked secret cannot be searched or indexed by its plaintext; a lookup by a secret needs a separate keyed hash column, which this add-on does not generate.
- Keys come from configuration, so in production from the secret store that feeds configuration (environment variables, a vault). A missing current key stops the host at startup, never at the first write.
- Envelopes are self-describing: the key id inside says which key opens them, so rotation needs no downtime and no flag day.
- Secrets never appear in logs: `LogFields` holds only ids, and a secret member of a command is `[JsonIgnore]` in any log of the command.

## ISecretProtector

File `src/{Name}.Shared/Application/ISecretProtector.cs`:

```csharp
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string envelope);
}
```

File `src/{Name}.Infrastructure/Secrets/EnvelopeSecretProtector.cs`:

```csharp
public class EnvelopeSecretProtector(IOptions<SecretsSettings> settings) : ISecretProtector
{
    public string Protect(string plaintext)
    {
        if (plaintext is null)
        {
            return null;
        }

        var keyId = settings.Value.CurrentKeyId;
        var dataKey = RandomNumberGenerator.GetBytes(SecretsConstants.KeyBytes);
        var (wrapNonce, wrappedKey, wrapTag) = Seal(KeyOf(keyId), dataKey);
        var (nonce, ciphertext, tag) = Seal(dataKey, Encoding.UTF8.GetBytes(plaintext));

        return string.Join('.', SecretsConstants.Version, keyId, Encode([.. wrapNonce, .. wrappedKey, .. wrapTag]), Encode(nonce), Encode(ciphertext), Encode(tag));
    }

    public string Unprotect(string envelope)
    {
        if (envelope is null)
        {
            return null;
        }

        var parts = envelope.Split('.');

        if (parts.Length != 6 || parts[0] != SecretsConstants.Version)
        {
            throw new InvalidOperationException("The value is not a secret envelope.");
        }

        var wrapped = Base64Url.DecodeFromChars(parts[2]);
        var dataKey = Open(KeyOf(parts[1]), wrapped[..SecretsConstants.NonceBytes], wrapped[SecretsConstants.NonceBytes..^SecretsConstants.TagBytes], wrapped[^SecretsConstants.TagBytes..]);

        return Encoding.UTF8.GetString(Open(dataKey, Base64Url.DecodeFromChars(parts[3]), Base64Url.DecodeFromChars(parts[4]), Base64Url.DecodeFromChars(parts[5])));
    }

    private byte[] KeyOf(string keyId)
    {
        return settings.Value.Keys.TryGetValue(keyId, out var key) ? Convert.FromBase64String(key) : throw new InvalidOperationException($"No secret key {keyId}.");
    }

    private static (byte[] Nonce, byte[] Ciphertext, byte[] Tag) Seal(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(SecretsConstants.NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[SecretsConstants.TagBytes];

        using var aesGcm = new AesGcm(key, SecretsConstants.TagBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        return (nonce, ciphertext, tag);
    }

    private static byte[] Open(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, SecretsConstants.TagBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static string Encode(byte[] bytes) => Base64Url.EncodeToString(bytes);
}
```

File `src/{Name}.Infrastructure/Secrets/SecretsConstants.cs`:

```csharp
public static class SecretsConstants
{
    public const string Version = "v1";
    public const string SecretAnnotation = "Mortis:Secret";
    public const int KeyBytes = 32;
    public const int NonceBytes = 12;
    public const int TagBytes = 16;
    public const int EnvelopeMaxLength = 2048;
}
```

File `src/{Name}.Infrastructure/Secrets/SecretsSettings.cs`:

```csharp
public class SecretsSettings
{
    public const string Section = "Secrets";

    public string CurrentKeyId { get; set; }
    public Dictionary<string, string> Keys { get; set; } = [];

    public bool HasUsableCurrentKey()
    {
        return CurrentKeyId is not null && Keys.TryGetValue(CurrentKeyId, out var key) && Convert.TryFromBase64String(key, new byte[SecretsConstants.KeyBytes], out var length) && length == SecretsConstants.KeyBytes;
    }
}
```

File `src/{Name}.Infrastructure/Secrets/SecretsModule.cs`:

```csharp
public static class SecretsModule
{
    public static IServiceCollection AddSecrets(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SecretsSettings>()
            .Bind(configuration.GetSection(SecretsSettings.Section))
            .Validate(settings => settings.HasUsableCurrentKey(), $"{SecretsSettings.Section}:CurrentKeyId must name a {SecretsConstants.KeyBytes}-byte base64 key in {SecretsSettings.Section}:Keys.")
            .ValidateOnStart();
        services.AddSingleton<ISecretProtector, EnvelopeSecretProtector>();

        return services;
    }
}
```

`InfrastructureModule` calls `services.AddSecrets(configuration);` (using `{Name}.Infrastructure.Secrets`). `ValidateOnStart` stops any host without a usable current key at startup. The development key lives only in each host's `appsettings.Development.json` (the API's, the Console's, and a new one for the Worker, which `Microsoft.NET.Sdk.Worker` copies on its own), for example `"Secrets": { "CurrentKeyId": "dev-1", "Keys": { "dev-1": "<32 random bytes, base64>" } }`; generate the key when the project is generated, never reuse one. A tampered envelope fails `Decrypt` with `AuthenticationTagMismatchException`, which surfaces as a 500 with a reference: it is a bug or an attack, never a caller's mistake.

## Persistence

File `src/{Name}.Infrastructure/Secrets/SecretPropertyExtensions.cs`:

```csharp
public static class SecretPropertyExtensions
{
    public static PropertyBuilder<string> IsSecret(this PropertyBuilder<string> propertyBuilder)
    {
        return propertyBuilder.HasAnnotation(SecretsConstants.SecretAnnotation, true).HasMaxLength(SecretsConstants.EnvelopeMaxLength);
    }

    public static void ProtectSecrets(this ModelBuilder modelBuilder, ISecretProtector secretProtector)
    {
        var converter = new ValueConverter<string, string>(plaintext => secretProtector.Protect(plaintext), envelope => secretProtector.Unprotect(envelope));

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetProperties()).Where(property => property.FindAnnotation(SecretsConstants.SecretAnnotation) is not null))
        {
            property.SetValueConverter(converter);
        }
    }
}
```

A configuration marks a column with `.IsSecret()` and needs no protector, so `ApplyConfigurationsFromAssembly` still finds it. `AppDbContext` takes the protector as a third constructor parameter (`ISecretProtector secretProtector`, usings `{Name}.Infrastructure.Secrets` and `{Name}.Shared.Application`) and calls `modelBuilder.ProtectSecrets(secretProtector);` right after `ApplyConfigurationsFromAssembly`. Every place that builds the context by hand passes one: `DesignTimeDbContextFactory` and the integration `PostgresFixture` pass `new EnvelopeSecretProtector(Options.Create(new SecretsSettings()))` (never called: migrations need only the column), `AppDbContextBuilder` in the unit tests passes one over `FakeSecretsSettings`. EF Core caches the model per context type, so the protector is a singleton and reads its keys when it runs. A secret column is compared by envelope, which differs on every write: never filter on it.

## The example

`Customer` gains `public string TaxNumber { get; private set; }` and

```csharp
    public void SetTaxNumber(string taxNumber)
    {
        TaxNumber = taxNumber;

        RaiseEvent(new TaxNumberSet(Id.ToString()));
    }
```

(`Events/TaxNumberSet.cs`: `public record TaxNumberSet(string Id) : EventBase;`, it carries no value; `CustomerConstants.TaxNumberMaxLength = 50`). `CustomerConfiguration`: `builder.Property(customer => customer.TaxNumber).IsSecret();`. With a query side, `CustomerView` gains `string TaxNumber = null` as its last parameter and `CustomerReader` selects it. `CustomerDto` gains `TaxNumber` in `Map` and `From`, so a caller who may read the customer reads it in plaintext through the port. File `src/{Name}.Application/Customers/SetCustomerTaxNumberCommand.cs` (registered with its validator in `ApplicationModule`):

```csharp
public record SetCustomerTaxNumberCommand : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

    [JsonIgnore] public Guid Id { get; set; }
    public string TaxNumber { get; set; }
}

public class SetCustomerTaxNumberCommandValidator : AbstractValidator<SetCustomerTaxNumberCommand>
{
    public SetCustomerTaxNumberCommandValidator()
    {
        RuleFor(command => command.TaxNumber).NotEmpty().MaximumLength(CustomerConstants.TaxNumberMaxLength);
    }
}

public class SetCustomerTaxNumberCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<SetCustomerTaxNumberCommand> commandPipeline) : BaseCommandHandler<SetCustomerTaxNumberCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(SetCustomerTaxNumberCommand command)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.SetTaxNumber(command.TaxNumber);
        customer.SetUpdatedAudit(command.Context.User);

        await customerRepository.UpdateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
```

`CustomersController`:

```csharp
    [HttpPut("{id:guid}/tax-number")]
    public async Task<IActionResult> SetTaxNumber([FromRoute] Guid id, [FromBody] SetCustomerTaxNumberCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }
```

## Tests

`test/{Name}.Tests/TestSupport/Infrastructure/Secrets/FakeSecretsSettings.cs`:

```csharp
public static class FakeSecretsSettings
{
    public const string FirstKeyId = "test-1";
    public const string SecondKeyId = "test-2";
    public const string FirstKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    public const string SecondKey = "HxwdHBsaGRgXFhUUExIREA8ODQwLCgkIBwYFBAMCAQA=";

    public static SecretsSettings Create(string currentKeyId = FirstKeyId) => new()
    {
        CurrentKeyId = currentKeyId,
        Keys = new Dictionary<string, string> { [FirstKeyId] = FirstKey, [SecondKeyId] = SecondKey }
    };
}
```

`test/{Name}.Tests/Infrastructure/Secrets/EnvelopeSecretProtectorTests.cs`:

```csharp
public class EnvelopeSecretProtectorTests
{
    [Fact]
    public void Protect_ThenUnprotect_Should_GiveBackThePlaintextFromAV1EnvelopeNamingTheCurrentKey()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));

        var envelope = secretProtector.Protect("TR-123");

        Assert.StartsWith($"{SecretsConstants.Version}.{FakeSecretsSettings.FirstKeyId}.", envelope);
        Assert.DoesNotContain("TR-123", envelope);
        Assert.Equal("TR-123", secretProtector.Unprotect(envelope));
    }

    [Fact]
    public void Protect_TwiceForOneValue_Should_GiveDifferentEnvelopes()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));

        Assert.NotEqual(secretProtector.Protect("TR-123"), secretProtector.Protect("TR-123"));
    }

    [Fact]
    public void Unprotect_AfterTheCurrentKeyChanged_Should_StillOpenAnEnvelopeOfTheOldKey()
    {
        var envelope = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create(FakeSecretsSettings.FirstKeyId))).Protect("TR-123");

        var plaintext = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create(FakeSecretsSettings.SecondKeyId))).Unprotect(envelope);

        Assert.Equal("TR-123", plaintext);
    }

    [Fact]
    public void Unprotect_WithAChangedCiphertext_Should_Throw()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));
        var parts = secretProtector.Protect("TR-123").Split('.');
        parts[4] = parts[4][0] == 'A' ? 'B' + parts[4][1..] : 'A' + parts[4][1..];

        Assert.ThrowsAny<CryptographicException>(() => secretProtector.Unprotect(string.Join('.', parts)));
    }

    [Fact]
    public void ProtectAndUnprotect_WithNull_Should_ReturnNull()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));

        Assert.Equal((null, null), (secretProtector.Protect(null), secretProtector.Unprotect(null)));
    }

    [Theory]
    [InlineData("test-1", true)]
    [InlineData("missing", false)]
    [InlineData(null, false)]
    public void HasUsableCurrentKey_Should_RequireAThirtyTwoByteKeyUnderTheCurrentId(string currentKeyId, bool expected)
    {
        Assert.Equal(expected, FakeSecretsSettings.Create(currentKeyId).HasUsableCurrentKey());
    }
}
```

`test/{Name}.Tests/Infrastructure/Persistence/Customers/CustomerConfigurationTests.cs`:

```csharp
public class CustomerConfigurationTests
{
    [Fact]
    public void Model_Should_StoreTheTaxNumberAsAnEnvelopeThroughAConverter()
    {
        var property = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.TaxNumber))!;

        var converter = property.GetValueConverter();

        Assert.NotNull(converter);
        Assert.StartsWith(SecretsConstants.Version + ".", (string)converter.ConvertToProvider("TR-123"));
        Assert.Equal(SecretsConstants.EnvelopeMaxLength, property.GetMaxLength());
    }
}
```

`test/{Name}.Tests/Application/Customers/SetCustomerTaxNumberCommandHandlerTests.cs`:

```csharp
public class SetCustomerTaxNumberCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_SetTheTaxNumberAndRaiseAnEventWithoutIt()
    {
        var customer = FakeCustomer.Create();
        customer.ClearEvents();
        var commandHandler = new SetCustomerTaxNumberCommandHandler(CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(customer).WithUpdateAsync().Build(), CommandPipelineBuilder<SetCustomerTaxNumberCommand>.Create().WithValidator(new SetCustomerTaxNumberCommandValidator()).Build());

        var commandResult = await commandHandler.Handle(new SetCustomerTaxNumberCommand { Id = customer.Id, TaxNumber = "TR-123", Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal("TR-123", commandResult.Content.TaxNumber);
        Assert.Equal(customer.Id.ToString(), Assert.IsType<TaxNumberSet>(Assert.Single(customer.DomainEvents)).Id);
    }

    [Fact]
    public async Task Handle_WithoutATaxNumber_Should_FailValidation()
    {
        var commandHandler = new SetCustomerTaxNumberCommandHandler(CustomerRepositoryBuilder.Create().Build(), CommandPipelineBuilder<SetCustomerTaxNumberCommand>.Create().WithValidator(new SetCustomerTaxNumberCommandValidator()).Build());

        await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new SetCustomerTaxNumberCommand { Id = Guid.CreateVersion7() }));
    }
}
```

`scripts/conformance/secrets.sh` proves the database side over HTTP and SQL.

## Files this add-on changes

`Customer.cs`, `CustomerConstants.cs`, `CustomerView.cs`, `CustomerReader.cs`, `CustomerDto.cs`, `CustomerConfiguration.cs`, `CustomersController.cs`, `AppDbContext.cs`, `DesignTimeDbContextFactory.cs`, `ApplicationModule.cs`, `InfrastructureModule.cs`, each host's `appsettings.Development.json`, the tests' `AppDbContextBuilder` and `PostgresFixture`; the regenerated migration adds `customers.tax_number`.

## Namespaces and usings

@@usings secrets.md@@

## Decision record

`docs/adr/0028-secrets-at-rest.md`, in `project-docs-adr.md`.
