using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform.Logging;

[ProviderAlias("ColoredConsole")]
internal sealed class ColoredConsoleLoggerProvider(LoggingSource loggingSource) : ILoggerProvider, ILogger
{
    private static readonly Dictionary<LogLevel, ConsoleColor> LogLevelColors = new Dictionary<LogLevel, ConsoleColor>
    {
        [LogLevel.Trace] = ConsoleColor.DarkGray,
        [LogLevel.Debug] = ConsoleColor.DarkGray,
        [LogLevel.Warning] = ConsoleColor.Yellow,
        [LogLevel.Error] = ConsoleColor.Red,
        [LogLevel.Critical] = ConsoleColor.Red,
    };

    private static readonly Dictionary<LoggingSource, string> LoggingSourcePrefixes = new Dictionary<LoggingSource, string>
    {
        [LoggingSource.Leap] = string.Empty,
        [LoggingSource.Aspire] = "[Aspire] ",
    };

    // The logger is accessed concurrently so we need to synchronize access to the console
    private static readonly object WriteLock = new object();

    public ILogger CreateLogger(string categoryName)
    {
        return this;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);

        var mustResetTerminalForegroundColor = false;

        lock (WriteLock)
        {
            if (LogLevelColors.TryGetValue(logLevel, out var color))
            {
                ConsoleExtensions.SetTerminalForeground(color);
                mustResetTerminalForegroundColor = true;
            }

            Console.Write(LoggingSourcePrefixes[loggingSource]);
            Console.WriteLine(message);

            if (exception != null)
            {
                Console.WriteLine(exception.Demystify());
            }

            if (mustResetTerminalForegroundColor)
            {
                ConsoleExtensions.ResetTerminalForegroundColor();
            }
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return this;
    }

    public void Dispose()
    {
    }
}