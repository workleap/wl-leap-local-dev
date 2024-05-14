namespace Leap.Cli.Platform;

internal static class ConsoleExtensions
{
    private static readonly Lazy<bool> LazyIsConsoleRedirectionSupported = new Lazy<bool>(() =>
    {
        try
        {
            _ = Console.IsOutputRedirected;
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    });

    private static bool IsConsoleRedirectionSupported => LazyIsConsoleRedirectionSupported.Value;

    public static void SetTerminalForeground(ConsoleColor color)
    {
        if (IsConsoleRedirectionSupported)
        {
            Console.ForegroundColor = color;
        }
    }

    public static void ResetTerminalForegroundColor()
    {
        if (IsConsoleRedirectionSupported)
        {
            Console.ResetColor();
        }
    }
}