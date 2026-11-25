using Boilerplate.Infrastructure.Persistence.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Boilerplate.Tests.TestSupport.Api.Idempotency;

public class IdempotencyStoreBuilder
{
    private readonly Mock<IdempotencyStore> _mock = new(Mock.Of<IServiceScopeFactory>(), Options.Create(new IdempotencySettings()));

    public static IdempotencyStoreBuilder Create() => new();

    public List<string> Calls { get; } = [];

    public IdempotencyStoreBuilder WithStartAsync(IdempotencyRecord record)
    {
        _mock.Setup(store => store.StartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(record).Callback(() => Calls.Add(nameof(IdempotencyStore.StartAsync)));
        _mock.Setup(store => store.FinishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string, string, int, string, string>((_, _, statusCode, _, body) => Calls.Add($"{nameof(IdempotencyStore.FinishAsync)} {statusCode} {body}"));
        _mock.Setup(store => store.ForgetAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Callback(() => Calls.Add(nameof(IdempotencyStore.ForgetAsync)));

        return this;
    }

    public IdempotencyStore Build() => _mock.Object;
}
