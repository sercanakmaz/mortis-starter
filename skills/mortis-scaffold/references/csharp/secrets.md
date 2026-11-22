# Secrets at rest, C#

Applies when `addons` contains `secrets` and `database` is `postgres` (with MongoDB not offered yet: R14, a note). Projects `{Name}.Shared` (folder `Application/`), `{Name}.Infrastructure` (folder `Secrets/`, and the configurations that use it) and, with the example, the customer slice. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

## How it fits together

```
ISecretProtector (port)   Protect(plaintext) -> envelope text;  Unprotect(envelope) -> plaintext
envelope                  v1.{keyId}.{wrappedDataKey}.{nonce}.{ciphertext}.{tag}   (base64url parts)
  data key                a fresh 32-byte AES-256-GCM key per value
  key-encryption key      Secrets:Keys:{keyId} (32 bytes, base64), the current one Secrets:CurrentKeyId; it wraps the data key (AES-GCM)
persistence               builder.Property(x => x.TaxNumber).IsSecret(): an EF value converter around the protector
domain                    sees plaintext; the database and its backups see only envelopes
rotation                  add a key, make it current: new writes use it, old envelopes still open with the key they name;
                          the console command rewrap-secrets rewrites every envelope with the current key
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
```

The member continues:

```csharp
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

`SecretsConstants`: `Version = "v1"`, `KeyBytes = 32`, `NonceBytes = 12`, `TagBytes = 16`, `EnvelopeMaxLength = 2048` (a secret of up to about 1,400 characters). `SecretsSettings`: `CurrentKeyId`, `Keys` (a dictionary of key id to base64), section `Secrets`. `SecretsModule.AddSecrets` binds them with `ValidateOnStart` and a validation that the current key exists and is 32 bytes. A tampered envelope fails `Decrypt` with `AuthenticationTagMismatchException`, which surfaces as a 500 with a reference: it is a bug or an attack, never a caller's mistake.

## Persistence

File `src/{Name}.Infrastructure/Secrets/SecretPropertyExtensions.cs`:

```csharp
public static class SecretPropertyExtensions
{
    public static PropertyBuilder<string> IsSecret(this PropertyBuilder<string> propertyBuilder, ISecretProtector secretProtector)
    {
        return propertyBuilder
            .HasConversion(plaintext => secretProtector.Protect(plaintext), envelope => secretProtector.Unprotect(envelope))
            .HasMaxLength(SecretsConstants.EnvelopeMaxLength);
    }
}
```

`AppDbContext` receives `ISecretProtector` through its constructor and passes it to configurations that need it (`modelBuilder.ApplyConfiguration(new CustomerConfiguration(secretProtector))`). EF Core caches the model per context type, so the protector is a singleton and its keys are read when it runs, not when the model is built. A secret column is compared by envelope, which differs on every write: never filter on it.

## The example

`Customer` gains `public string TaxNumber { get; private set; }` and `public void SetTaxNumber(string taxNumber) { TaxNumber = taxNumber; RaiseEvent(new TaxNumberSet(Id.ToString())); }` (the event carries no value). `CustomerConfiguration`: `builder.Property(customer => customer.TaxNumber).IsSecret(secretProtector);`. `SetCustomerTaxNumberCommand` (`PUT /customers/{id}/tax-number`, body `{ "taxNumber": "..." }`) sets it; `CustomerDto` gains `TaxNumber`, so a signed-in caller who may read the customer reads it in plaintext through the port. The console command `rewrap-secrets` loads every customer with a tax number and saves it again, which re-encrypts with the current key.

## Tests

`EnvelopeSecretProtectorTests`: a round trip returns the plaintext; two protections of one value differ; an envelope made with an old key still opens after the current key changes; a changed character in the ciphertext throws; null stays null. `CustomerConfigurationTests` gains: `TaxNumber` has a value converter. At `testing` = `integration` and above, `SecretColumnTests` in the integration project: the column read with raw SQL starts with `v1.` and does not contain the plaintext. `scripts/conformance/secrets.sh` proves it over HTTP and SQL.

## Files this add-on changes

`Customer.cs`, `CustomerDto.cs`, `CustomerConfiguration.cs`, `CustomersController.cs`, `AppDbContext.cs`, `ApplicationModule.cs`, `InfrastructureModule.cs`, the Console's command list, `appsettings.json` and `appsettings.Development.json` (a development key, never the production one); a migration adds `customers.tax_number`.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `ISecretProtector.cs` | `{Name}.Shared.Application` | none |
| `EnvelopeSecretProtector.cs`, `SecretsModule.cs`, `SecretsSettings.cs`, `SecretsConstants.cs`, `SecretPropertyExtensions.cs` | `{Name}.Infrastructure.Secrets` | `{Name}.Shared.Application`, `Microsoft.EntityFrameworkCore.Metadata.Builders`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `System.Buffers.Text`, `System.Security.Cryptography`, `System.Text` |
| `SetCustomerTaxNumberCommand.cs` | `{Name}.Application.Customers` | `System.Text.Json.Serialization`, `{Name}.Domain.Aggregates.Customers`, `{Name}.Shared.Application`, `{Name}.Shared.Exceptions`, `FluentValidation` |

## Decision record

`docs/adr/0028-secrets-at-rest.md`, in `project-docs-adr.md`.
