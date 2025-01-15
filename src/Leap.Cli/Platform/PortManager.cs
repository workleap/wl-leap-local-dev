using System.Net;
using System.Net.Sockets;
using Leap.Cli.Dependencies;
using Leap.Cli.Dependencies.Azurite;

namespace Leap.Cli.Platform;

internal sealed class PortManager : IPortManager
{
    private static readonly HashSet<int> ReservedPorts = new HashSet<int>
    {
        // Avoiding standard ports to prevent conflicts
        80,
        443,

        // Leap internals
        Constants.LeapReverseProxyPort,
        Constants.LeapAzureCliProxyPort,

        // Third-party dependencies
        MongoDependencyHandler.MongoPort,
        RedisDependencyHandler.RedisPort,
        FusionAuthDependencyHandler.FusionAuthPort,
        PostgresDependencyHandler.HostPostgresPort,
        SqlServerDependencyHandler.HostSqlServerPort,
        AzuriteConstants.BlobPort,
        AzuriteConstants.QueuePort,
        AzuriteConstants.TablePort,
    };

    public int GetRandomAvailablePort(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // https://stackoverflow.com/a/150974/825695
            using var listener = new TcpListener(IPAddress.Loopback, port: 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            if (ReservedPorts.Add(port))
            {
                return port;
            }
        }
    }

    public bool IsPortAvailable(int port)
    {
        using var listener = new TcpListener(IPAddress.Loopback, port);

        try
        {
            listener.Start();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool IsPortInValidRange(int port)
    {
        return port is > 0 and <= 65535;
    }
}