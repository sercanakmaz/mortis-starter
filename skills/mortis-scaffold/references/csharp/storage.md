# File storage, C#

Applies when `addons` contains `storage`. Projects `{Name}.Shared` (folder `Application/`), `{Name}.Infrastructure` (folder `Storage/`), the API and, with the example, the product slice. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

Package, in `{Name}.Infrastructure`: `AWSSDK.S3` (S3 and MinIO).

## How it fits together

```
IFileStorage (port)    PutAsync(key, content, contentType) / OpenAsync(key) -> StoredFile or null / DeleteAsync(key)
adapters               S3FileStorage (AWS S3, MinIO with ServiceUrl + path style), LocalDiskFileStorage (a root folder)
choice                 Storage:Provider = S3 | LocalDisk (LocalDisk by default; S3 in compose with MinIO)
keys                   {plural}/{tenant}/{id}/{name}: built by the application, never from a client's file name
example                PUT /products/{id}/image (multipart) -> AttachProductImageCommand -> product.ImageKey
                       GET /products/{id}/image -> GetProductImageCommand -> the stream, with its content type
```

- The aggregate stores the key, never the bytes; the bytes live behind the port.
- A key is built from ids the application owns, so a client cannot write outside its folder; the local adapter also refuses any key that resolves outside its root.
- The upload and the row are not one transaction: the handler writes the file first and the key second, so a failure leaves at worst an orphan file, never a row pointing at nothing. A cleanup of orphans is the project's own job.
- Uploads are limited by `Storage:MaxBytes` (10 MB by default) and a list of allowed content types (`Storage:AllowedContentTypes`), checked by the command's validator.

## IFileStorage

File `src/{Name}.Shared/Application/IFileStorage.cs`:

```csharp
public interface IFileStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<StoredFile> OpenAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
```

File `src/{Name}.Shared/Application/StoredFile.cs`: `public sealed record StoredFile(Stream Content, string ContentType, long Length);` (the caller disposes `Content`).

## The adapters

File `src/{Name}.Infrastructure/Storage/S3FileStorage.cs`:

```csharp
public class S3FileStorage(IAmazonS3 amazonS3, IOptions<StorageSettings> settings) : IFileStorage
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await amazonS3.PutObjectAsync(new PutObjectRequest { BucketName = settings.Value.Bucket, Key = key, InputStream = content, ContentType = contentType }, cancellationToken);
    }

    public async Task<StoredFile> OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await amazonS3.GetObjectAsync(settings.Value.Bucket, key, cancellationToken);

            return new StoredFile(response.ResponseStream, response.Headers.ContentType, response.ContentLength);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await amazonS3.DeleteObjectAsync(settings.Value.Bucket, key, cancellationToken);
    }
}
```

File `src/{Name}.Infrastructure/Storage/LocalDiskFileStorage.cs`:

```csharp
public class LocalDiskFileStorage(IOptions<StorageSettings> settings) : IFileStorage
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        await File.WriteAllTextAsync(path + StorageConstants.ContentTypeSuffix, contentType, cancellationToken);
    }

    public async Task<StoredFile> OpenAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathOf(key);

        if (!File.Exists(path))
        {
            return null;
        }

        var contentType = await File.ReadAllTextAsync(path + StorageConstants.ContentTypeSuffix, cancellationToken);

        return new StoredFile(File.OpenRead(path), contentType, new FileInfo(path).Length);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        File.Delete(PathOf(key));
        File.Delete(PathOf(key) + StorageConstants.ContentTypeSuffix);

        return Task.CompletedTask;
    }

    private string PathOf(string key)
    {
        var root = Path.GetFullPath(settings.Value.Root);
        var path = Path.GetFullPath(Path.Combine(root, key));

        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : throw new InvalidParameterException(nameof(key), "outside the storage root");
    }
}
```

File `src/{Name}.Infrastructure/Storage/StorageModule.cs` registers the adapter `Storage:Provider` names: for `S3`, `services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(new BasicAWSCredentials(s.AccessKey, s.SecretKey), new AmazonS3Config { ServiceURL = s.ServiceUrl, ForcePathStyle = true }))` (the endpoint and path style make it MinIO-ready; on AWS leave `ServiceUrl` empty and use the default credential chain); for `LocalDisk`, `LocalDiskFileStorage`. `StorageSettings`: `Provider`, `Root` (`./storage`), `Bucket`, `ServiceUrl`, `AccessKey`, `SecretKey`, `MaxBytes` (10485760), `AllowedContentTypes` (`image/png`, `image/jpeg`). `docker-compose.yml` gains a `minio` service and a bucket-creating `minio-init`.

## The example

`Product` gains `public string ImageKey { get; private set; }` and `public void AttachImage(string imageKey) { ImageKey = imageKey; RaiseEvent(new ImageAttached(Id.ToString(), imageKey)); }`; `ProductDto` gains `HasImage`.

File `src/{Name}.Application/Products/AttachProductImageCommand.cs`:

```csharp
public record AttachProductImageCommand : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
    [JsonIgnore] public Guid Id { get; set; }
    [JsonIgnore] public Stream Content { get; set; }
    public string ContentType { get; set; }
    public long Length { get; set; }
}

public class AttachProductImageCommandHandler(
    IProductRepository productRepository,
    IFileStorage fileStorage,
    CommandPipeline<AttachProductImageCommand> commandPipeline) : BaseCommandHandler<AttachProductImageCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(AttachProductImageCommand command)
    {
        var product = await productRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Product));
        var imageKey = ProductConstants.ImageKey(command.Context.TenantId, product.Id);

        await fileStorage.PutAsync(imageKey, command.Content, command.ContentType);

        product.AttachImage(imageKey);
        product.SetUpdatedAudit(command.Context.User);

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
```

Its validator checks `Length` against `MaxBytes` and `ContentType` against the allowed list (both from `IOptions<StorageSettings>` passed to the validator's constructor). `ProductConstants.ImageKey(tenantId, id)` is `$"products/{tenantId ?? "none"}/{id}/image"`. `GetProductImageCommand` returns the `StoredFile` (a `not_found` when the product has no image or the file is gone). `ProductsController`:

```csharp
    [HttpPut("{id:guid}/image")]
    [RequestSizeLimit(StorageConstants.RequestLimitBytes)]
    public async Task<IActionResult> AttachImage([FromRoute] Guid id, IFormFile file)
    {
        await using var content = file.OpenReadStream();
        var commandResult = await commandDispatcher.Send(new AttachProductImageCommand { Id = id, Content = content, ContentType = file.ContentType, Length = file.Length });

        return Ok(commandResult.Content);
    }

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new GetProductImageCommand(id));

        return File(commandResult.Content.Content, commandResult.Content.ContentType);
    }
```

## Tests

`LocalDiskFileStorageTests` (a temporary folder): put then open returns the same bytes and content type; open of a missing key returns null; a key with `..` that leaves the root is refused. `AttachProductImageCommandHandlerTests` with a `FileStorageBuilder` mock: the file is put before the row is updated (recorded call order), and a missing product puts nothing. The validator refuses a too-large file and an unlisted content type. At `testing` = `integration` and above, `S3FileStorageTests` in the integration project runs against a MinIO container (`Testcontainers.Minio`). `scripts/conformance/storage.sh` uploads and reads back over HTTP.

## Files this add-on changes

`Product.cs`, `ProductDto.cs`, `ProductConstants.cs`, `ProductsController.cs`, `ApplicationModule.cs`, `InfrastructureModule.cs`, `appsettings.json` (`"Storage": { "Provider": "LocalDisk", "Root": "./storage" }`), `docker-compose.yml`; a migration adds `products.image_key`.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `IFileStorage.cs`, `StoredFile.cs` | `{Name}.Shared.Application` | none |
| `S3FileStorage.cs`, `LocalDiskFileStorage.cs`, `StorageModule.cs`, `StorageSettings.cs`, `StorageConstants.cs` | `{Name}.Infrastructure.Storage` | `{Name}.Shared.Application`, `{Name}.Shared.Exceptions`, `Amazon.Runtime`, `Amazon.S3`, `Amazon.S3.Model`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `System.Net` |
| `AttachProductImageCommand.cs`, `GetProductImageCommand.cs` | `{Name}.Application.Products` | `System.Text.Json.Serialization`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Application`, `{Name}.Shared.Exceptions`, `FluentValidation` |

## Decision record

`docs/adr/0027-file-storage-port.md`, in `project-docs-adr.md`.
