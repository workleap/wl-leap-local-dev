namespace Leap.Cli.Platform;

internal static class HostNameResolver
{
    public static string ReplaceLocalhostWithContainerHost(string value)
    {
        const string hostName = "host.docker.internal";

        return value.Replace("localhost", hostName, StringComparison.OrdinalIgnoreCase)
            .Replace("127.0.0.1", hostName, StringComparison.Ordinal)
            .Replace("[::1]", hostName, StringComparison.Ordinal);
    }
}