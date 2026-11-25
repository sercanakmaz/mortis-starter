using Microsoft.Extensions.Logging;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Messaging;

public class RecordingLogger<TCategory> : ILogger<TCategory>
{
    public List<(LogLevel Level, Exception Exception)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        Entries.Add((logLevel, exception));
    }
}
