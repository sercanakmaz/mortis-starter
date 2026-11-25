using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text;

namespace Boilerplate.Infrastructure.Logging;

public sealed class JsonLineFormatter() : ConsoleFormatter(LoggingConstants.FormatterName)
{
    private static readonly string Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
    {
        var fields = new Dictionary<string, object>();

        scopeProvider?.ForEachScope((scope, collected) =>
        {
            if (scope is not IEnumerable<KeyValuePair<string, object>> pairs) return;

            foreach (var pair in pairs.Where(pair => pair.Key != LoggingConstants.OriginalFormat))
            {
                collected[pair.Key] = pair.Value;
            }
        }, fields);

        if (logEntry.State is IEnumerable<KeyValuePair<string, object>> statePairs)
        {
            foreach (var pair in statePairs.Where(pair => pair.Key != LoggingConstants.OriginalFormat))
            {
                fields[pair.Key] = pair.Value;
            }
        }

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString(LoggingConstants.CorrelationId, Convert.ToString(fields.GetValueOrDefault(LoggingConstants.CorrelationId), CultureInfo.InvariantCulture) is { Length: > 0 } correlationId ? correlationId : null);
            writer.WriteString("Timestamp", DateTime.UtcNow);
            writer.WriteString("Level", logEntry.LogLevel.ToString());
            writer.WriteString("Service", LoggingConstants.Service);
            writer.WriteString("Version", Version);
            writer.WriteString("Category", logEntry.Category);
            writer.WriteString("Message", logEntry.Formatter(logEntry.State, logEntry.Exception));
            writer.WriteString(LoggingConstants.TraceId, Activity.Current?.TraceId.ToString());
            writer.WriteString(LoggingConstants.SpanId, Activity.Current?.SpanId.ToString());

            foreach (var (key, value) in fields.Where(field => field.Key != LoggingConstants.CorrelationId))
            {
                if (value is null)
                {
                    writer.WriteNull(key);
                }
                else if (value is bool or int or long or double or decimal)
                {
                    writer.WritePropertyName(key);
                    JsonSerializer.Serialize(writer, value);
                }
                else
                {
                    writer.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
                }
            }

            if (logEntry.Exception is not null)
            {
                writer.WriteString("Exception", logEntry.Exception.ToString());
            }

            writer.WriteEndObject();
        }

        textWriter.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }
}
