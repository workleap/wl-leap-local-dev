using Microsoft.Extensions.Hosting;

namespace Workleap.Extensions.Leap;

public static class HostExtensions
{
    public static bool IsLocal(this IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        return hostEnvironment.IsEnvironment("Local");
    }
}