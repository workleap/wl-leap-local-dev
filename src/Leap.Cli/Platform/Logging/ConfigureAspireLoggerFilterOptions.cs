using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Platform.Logging;

internal sealed class ConfigureAspireLoggerFilterOptions(IOptions<LeapGlobalOptions> leapGlobalOptions) : IConfigureOptions<LoggerFilterOptions>
{
    public void Configure(LoggerFilterOptions options)
    {
        switch (leapGlobalOptions.Value.Verbosity)
        {
            case LoggerVerbosity.Quiet:
                options.AddFilter<ColoredConsoleLoggerProvider>("System.Net.Http", LogLevel.Warning);
                options.AddFilter<ColoredConsoleLoggerProvider>("Aspire", LogLevel.Warning);
                options.AddFilter<ColoredConsoleLoggerProvider>("Microsoft", LogLevel.Warning);
                break;
            case LoggerVerbosity.Normal:
                options.AddFilter<ColoredConsoleLoggerProvider>("System.Net.Http", LogLevel.Warning);
                options.AddFilter<ColoredConsoleLoggerProvider>("Aspire", LogLevel.Warning);
                options.AddFilter<ColoredConsoleLoggerProvider>("Microsoft", LogLevel.Warning);
                break;
            case LoggerVerbosity.Diagnostic:
                options.AddFilter<ColoredConsoleLoggerProvider>("System.Net.Http", LogLevel.Information);
                options.AddFilter<ColoredConsoleLoggerProvider>("Aspire", LogLevel.Trace);
                options.AddFilter<ColoredConsoleLoggerProvider>("Microsoft", LogLevel.Trace);

                // We don't need to print that we are authenticated at each Aspire dashboard page load
                options.AddFilter<ColoredConsoleLoggerProvider>("Aspire.Hosting.Dashboard.ResourceServiceApiKeyAuthenticationHandler", LogLevel.Warning);
                break;
        }

        // Remove noise from unsuccessful healthchecks in CLI console output
        options.AddFilter<ColoredConsoleLoggerProvider>("Microsoft.Extensions.Diagnostics.HealthChecks", LogLevel.Critical);
    }
}