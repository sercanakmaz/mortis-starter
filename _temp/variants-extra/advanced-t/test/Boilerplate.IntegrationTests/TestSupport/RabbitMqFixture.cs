using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;

namespace Boilerplate.IntegrationTests.TestSupport;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private RabbitMqContainer _container;

    public string Uri { get; private set; }
    public string Unavailable { get; private set; }

    public async Task InitializeAsync()
    {
        Uri = Environment.GetEnvironmentVariable(IntegrationConstants.RabbitMqVariable);

        if (Uri == IntegrationConstants.SkipValue)
        {
            Unavailable = $"{IntegrationConstants.RabbitMqVariable} is {IntegrationConstants.SkipValue}.";
            return;
        }

        if (!string.IsNullOrEmpty(Uri))
        {
            return;
        }

        try
        {
            _container = new RabbitMqBuilder(IntegrationConstants.RabbitMqImage).Build();
            await _container.StartAsync();
            Uri = _container.GetConnectionString();
        }
        catch (Exception exception) when (exception is DockerUnavailableException or HttpRequestException or TimeoutException or ArgumentException)
        {
            Unavailable = $"No RabbitMQ: set {IntegrationConstants.RabbitMqVariable} or start Docker ({exception.GetType().Name}).";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
