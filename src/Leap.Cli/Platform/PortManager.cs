using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Leap.Cli.Configuration;
using Leap.Cli.Dependencies;
using Leap.Cli.Dependencies.Azurite;

namespace Leap.Cli.Platform;

internal sealed class PortManager : IPortManager
{
    private static readonly HashSet<int> ReservedPorts = new HashSet<int>
    {
        // Avoidind standard ports to prevent conflicts
        80,
        443,

        // Leap internals
        Constants.LeapReverseProxyPort,

        // Third-party dependencies
        MongoDependencyHandler.MongoPort,
        RedisDependencyHandler.RedisPort,
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

            var listener = new TcpListener(IPAddress.Any, port: 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            if (ReservedPorts.Add(port))
            {
                return port;
            }
        }
    }

    // TODO don't register ports provided by users. It can be legit to reuse it multiple times on different hosts and paths
    public bool TryRegisterPort(int port, [NotNullWhen(false)] out InvalidPortReason? reason)
    {
        if (!this.IsPortInValidRange(port))
        {
            reason = InvalidPortReason.OutOfBounds;
            return false;
        }

        if (!ReservedPorts.Add(port))
        {
            reason = InvalidPortReason.AlreadyUsed;
            return false;
        }

        reason = null;
        return true;
    }

    public bool IsPortInValidRange(int port)
    {
        return port is > 0 and <= 65535;
    }
}