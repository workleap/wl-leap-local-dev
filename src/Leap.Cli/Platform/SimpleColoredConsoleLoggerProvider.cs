using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

[ProviderAlias("SimpleColoredConsole")]
internal sealed class SimpleColoredConsoleLoggerProvider : ILoggerProvider, ILogger
{
    private static readonly Dictionary<LogLevel, ConsoleColor> LogLevelColors = new Dictionary<LogLevel, ConsoleColor>
    {
        [LogLevel.Trace] = ConsoleColor.DarkGray,
        [LogLevel.Debug] = ConsoleColor.DarkGray,
        [LogLevel.Warning] = ConsoleColor.Yellow,
        [LogLevel.Error] = ConsoleColor.Red,
        [LogLevel.Critical] = ConsoleColor.Red,
    };

    private static bool? _isConsoleRedirectionSupported;

    private static bool IsConsoleRedirectionSupported
    {
        get
        {
            if (!_isConsoleRedirectionSupported.HasValue)
            {
                try
                {
                    _ = Console.IsOutputRedirected;
                    _isConsoleRedirectionSupported = true;
                }
                catch (PlatformNotSupportedException)
                {
                    _isConsoleRedirectionSupported = false;
                }
            }

            return _isConsoleRedirectionSupported.Value;
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return this;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);

        var mustResetTerminalForegroundColor = false;

        if (LogLevelColors.TryGetValue(logLevel, out var color))
        {
            SetTerminalForeground(color);
            mustResetTerminalForegroundColor = true;
        }

        Console.WriteLine(message);

        if (exception != null)
        {
            Console.WriteLine(exception);
        }

        if (mustResetTerminalForegroundColor)
        {
            ResetTerminalForegroundColor();
        }
    }

    private static void SetTerminalForeground(ConsoleColor color)
    {
        if (IsConsoleRedirectionSupported)
        {
            Console.ForegroundColor = color;
        }
    }

    private static void ResetTerminalForegroundColor()
    {
        if (IsConsoleRedirectionSupported)
        {
            Console.ResetColor();
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