using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Boilerplate.Shared.Diagnostics;

public static class Telemetry
{
    public const string Name = "Boilerplate";
    public const string Succeeded = "succeeded";
    public const string Refused = "refused";
    public const string Failed = "failed";

    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);
    public static readonly Histogram<double> CommandDuration = Meter.CreateHistogram<double>("commands.duration", "ms", "Time a command took, by command and outcome.");
    public static readonly Counter<long> ConsumerFailures = Meter.CreateCounter<long>("consumers.failures", description: "Event consumer runs that threw, by consumer.");
}
