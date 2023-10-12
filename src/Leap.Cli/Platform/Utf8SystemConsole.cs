using System.CommandLine.IO;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Leap.Cli.Platform;

[ExcludeFromCodeCoverage]
internal sealed class Utf8SystemConsole : SystemConsole
{
    static Utf8SystemConsole()
    {
        var isInteractive = !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected;
        if (isInteractive)
        {
            // Enables a wider range of characters such as emojis
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
        }
    }
}