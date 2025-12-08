namespace Leap.Cli.Aspire;

/// <summary>
/// Helps to set the reverse proxy URL for a service at the first position in the dashboard based on the ordering rules of .NET Aspire:
/// https://github.com/dotnet/aspire/blob/v9.0.0-rc.1.24511.1/src/Aspire.Dashboard/Model/ResourceEndpointHelpers.cs#L33-L42
/// </summary>
internal static class EndpointNameHelper
{
    public static string GetReverseProxyEndpointName()
    {
        return "http-0"; // The first position in the dashboard
    }

    public static string GetLocalhostEndpointName(int index = 0)
    {
        return $"http-{index + 1}";
    }
}