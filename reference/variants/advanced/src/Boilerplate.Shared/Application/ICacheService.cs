namespace Boilerplate.Shared.Application;

public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string key, IReadOnlyCollection<string> tags, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken = default);
    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);
}
