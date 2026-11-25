using Boilerplate.Shared.Application;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

public class PassThroughCacheService : ICacheService
{
    public List<string> RemovedTags { get; } = [];

    public Task<T> GetOrCreateAsync<T>(string key, IReadOnlyCollection<string> tags, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken = default) => load(cancellationToken);

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        RemovedTags.Add(tag);

        return Task.CompletedTask;
    }
}
