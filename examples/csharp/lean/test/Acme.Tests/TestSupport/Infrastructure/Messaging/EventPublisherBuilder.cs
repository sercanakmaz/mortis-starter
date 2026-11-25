using Acme.Infrastructure.Messaging;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Tests.TestSupport.Infrastructure.Messaging;

public class EventPublisherBuilder
{
    private readonly ServiceCollection _services = new();
    private readonly RecordingLogger<EventPublisher> _logger = new();

    public static EventPublisherBuilder Create() => new();

    public EventPublisherBuilder WithUnitOfWork(IUnitOfWork unitOfWork)
    {
        _services.AddScoped(_ => unitOfWork);
        return this;
    }

    public EventPublisherBuilder WithEventConsumer<TConsumer>(TConsumer eventConsumer, int retryCount) where TConsumer : class, IEventConsumer
    {
        _services.AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(retryCount));
        _services.AddSingleton(eventConsumer);
        return this;
    }

    public RecordingLogger<EventPublisher> BuildLogger() => _logger;

    public EventPublisher Build()
    {
        _services.AddEventConsumerRegistry();

        var serviceProvider = _services.BuildServiceProvider();

        return new EventPublisher(serviceProvider.GetRequiredService<IServiceScopeFactory>(), serviceProvider.GetRequiredService<EventConsumerRegistry>(), _logger);
    }
}
