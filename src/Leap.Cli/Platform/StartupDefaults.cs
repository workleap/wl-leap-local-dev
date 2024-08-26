using System.Globalization;
using System.Text;

namespace Leap.Cli.Platform;

internal static class StartupDefaults
{
    public static void SetEnvironmentVariables()
    {
        ClearDotNetConflictingEnvironmentVariables();
        SetNodeExtraCaCertsForUserIfNotAlreadySet();
    }

    private static void ClearDotNetConflictingEnvironmentVariables()
    {
        // Overridden environment variables due to a conflict with a script from ShareGate Teams management that sets user-scoped environment variables.
        // https://dev.azure.com/sharegate/Sharegate.Gravt/_git/Sharegate.Gravt?path=/src/devtools/local/Set-Profile.ps1&version=GC5f80822ba73f9adc33b5c3ad847599326ace58dc&line=64&lineEnd=66&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents
        // Leap uses an SSL certificate setup (CRT + key file), while the other script assumes PFX certificate usage.
        // This ensures our SSL configuration takes precedence in ASP.NET Core web apps started by Leap.
        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path", null, EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password", null, EnvironmentVariableTarget.Process);
    }

    private static void SetNodeExtraCaCertsForUserIfNotAlreadySet()
    {
        // .NET on Unix-like systems does not support per-user and per-machine environment variables
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("NODE_EXTRA_CA_CERTS", EnvironmentVariableTarget.User) == null)
        {
            // Helps Node.js apps trust our certificate authority which contains our self-signed certificates with other root CAs.
            // It is required for apps such as Microsoft Azure Storage Explorer to communicate with our Azurite instance over HTTPS.
            Environment.SetEnvironmentVariable("NODE_EXTRA_CA_CERTS", Constants.LeapCertificateAuthorityFilePath, EnvironmentVariableTarget.User);
        }
    }

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