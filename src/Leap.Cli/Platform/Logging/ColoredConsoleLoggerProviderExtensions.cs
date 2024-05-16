using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform.Logging;

internal static class ColoredConsoleLoggerProviderExtensions
{
    public static ILoggingBuilder AddColoredConsoleLogger(this ILoggingBuilder builder, LoggingSource loggingSource)
    {
        builder.AddProvider(new ColoredConsoleLoggerProvider(loggingSource));

        if (loggingSource == LoggingSource.Leap)
        {
            builder.Services.ConfigureOptions<ConfigureLeapLoggerFilterOptions>();
        }
        else if (loggingSource == LoggingSource.Aspire)
        {
            builder.Services.ConfigureOptions<ConfigureAspireLoggerFilterOptions>();
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(loggingSource), loggingSource, "Unknown logging source.");
        }

        return builder;
    }
}