using System.Globalization;
using System.Text;

namespace Leap.Cli.Platform;

internal static class ConsoleDefaults
{
    public static void SetInvariantCulture()
    {
        // So we never have to worry about formatting dates and numbers in a culture-specific way while doing string interpolation
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    public static void SetUtf8Encoding()
    {
        // Enables a wider range of characters such as emojis
        var isInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected;
        if (isInteractive)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
        }
    }
}