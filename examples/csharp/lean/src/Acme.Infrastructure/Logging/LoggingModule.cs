using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Acme.Infrastructure.Logging;

public static class LoggingModule
{
    public static ILoggingBuilder AddJsonLogging(this ILoggingBuilder logging, bool toStandardError = false)
    {
        logging.ClearProviders();
        logging.AddConsole(options =>
        {
            options.FormatterName = LoggingConstants.FormatterName;

            if (toStandardError)
            {
                options.LogToStandardErrorThreshold = LogLevel.Trace;
            }
        });
        logging.AddConsoleFormatter<JsonLineFormatter, ConsoleFormatterOptions>();

        return logging;
    }
}
