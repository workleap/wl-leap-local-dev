using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Aspire;

internal sealed class ResourceLoggerProvider(ILogger underlyingLogger) : ILoggerProvider, ILogger
{
    private static readonly Dictionary<LogLevel, string> LogLevelStrings = new Dictionary<LogLevel, string>
    {
        [LogLevel.None] = "NON",
        [LogLevel.Trace] = "TRC",
        [LogLevel.Debug] = "DBG",
        [LogLevel.Information] = "INF",
        [LogLevel.Warning] = "WRN",
        [LogLevel.Error] = "ERR",
        [LogLevel.Critical] = "CRT",
    };

    public ILogger CreateLogger(string categoryName)
    {
        return this;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return underlyingLogger.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return underlyingLogger.IsEnabled(logLevel);
    }

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "DateTime.Now is used to display the local time to the user")]
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = $"[{DateTime.Now:HH:mm:ss:ffff} {LogLevelStrings[logLevel]}] {formatter(state, exception)}";
        underlyingLogger.Log(logLevel, exception, "{FormattedMessage}", message);
    }

    public void Dispose()
    {
    }
}

internal static class ResourceLoggerProviderExtensions
{
    public static ILoggingBuilder AddResourceLogger(this ILoggingBuilder builder, ILogger logger)
    {
        return builder.AddProvider(new ResourceLoggerProvider(logger));
    }
}