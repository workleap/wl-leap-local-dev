using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Platform.Logging;

internal sealed class ConfigureLeapLoggerFilterOptions(IOptions<LeapGlobalOptions> leapGlobalOptions) : IConfigureOptions<LoggerFilterOptions>
{
    public void Configure(LoggerFilterOptions options)
    {
        switch (leapGlobalOptions.Value.Verbosity)
        {
            case LoggerVerbosity.Quiet:
                options.AddFilter<ColoredConsoleLoggerProvider>(nameof(Leap), LogLevel.Information);
                options.AddFilter<ColoredConsoleLoggerProvider>("System.Net.Http", LogLevel.Warning);
                break;
            case LoggerVerbosity.Normal:
                options.AddFilter<ColoredConsoleLoggerProvider>(nameof(Leap), LogLevel.Trace);
                options.AddFilter<ColoredConsoleLoggerProvider>("System.Net.Http", LogLevel.Warning);
                break;
            case LoggerVerbosity.Diagnostic:
                options.AddFilter<ColoredConsoleLoggerProvider>(nameof(Leap), LogLevel.Trace);
                options.AddFilter<ColoredConsoleLoggerProvider>("System.Net.Http", LogLevel.Information);
                break;
        }
    }
}