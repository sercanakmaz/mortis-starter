using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Shared.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Boilerplate.Infrastructure.Diagnostics;

public static class ObservabilityModule
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, Action<TracerProviderBuilder> hostTracing = null)
    {
        var settings = configuration.GetSection(ObservabilitySettings.Section).Get<ObservabilitySettings>() ?? new ObservabilitySettings();

        services.Configure<ObservabilitySettings>(configuration.GetSection(ObservabilitySettings.Section));
        services.AddSingleton<OutboxLagProbe>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OutboxLagProbe>());
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(ObservabilityConstants.DatabaseCheck, tags: [ObservabilityConstants.ReadyTag])
            .AddCheck<OutboxHealthCheck>(ObservabilityConstants.OutboxCheck, tags: [ObservabilityConstants.ReadyTag])
            .AddCheck<RabbitMqHealthCheck>(ObservabilityConstants.BrokerCheck, tags: [ObservabilityConstants.ReadyTag]);

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(Telemetry.Name))
            .WithTracing(tracing =>
            {
                tracing.AddSource(Telemetry.Name);
                hostTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics => metrics.AddMeter(Telemetry.Name));

        if (!string.IsNullOrWhiteSpace(settings.OtlpEndpoint))
        {
            openTelemetry.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(settings.OtlpEndpoint));
        }

        return services;
    }
}
