namespace Leap.Cli.Platform;

internal static class HostNameResolver
{
    // Inspired by .NET Aspire
    // https://github.com/dotnet/aspire/blob/v8.2.1/src/Aspire.Hosting/Dcp/ApplicationExecutor.cs#L1833
    public static string ReplaceLocalhostWithContainerHost(string value)
    {
        const string defaultContainerHostName = "host.docker.internal";

        return value.Replace("localhost", defaultContainerHostName, StringComparison.OrdinalIgnoreCase)
            .Replace("127.0.0.1", defaultContainerHostName, StringComparison.Ordinal)
            .Replace("[::1]", defaultContainerHostName, StringComparison.Ordinal);
    }
}