using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

    public bool TryRegisterPort(int port, [NotNullWhen(false)] out InvalidPortReason? reason)
    {
        if (!this.IsLocalhostTcpPortAvailable(port))
        {
            reason = InvalidPortReason.InUse;
            return false;
        }

        if (!this.IsPortInValidRange(port))
        {
            reason = InvalidPortReason.OutOfBounds;
            return false;
        }

        if (!ReservedPorts.Add(port))
        {
            reason = InvalidPortReason.ReservedByLeap;
            return false;
        }

        reason = null;
        return true;
    }

    public bool IsPortInValidRange(int port)
    {
        return port is > 0 and <= 65535;
    }

    private bool IsLocalhostTcpPortAvailable(int port)
    {
        // Check both address families to avoid surprises
        return this.IsTcpListenerAvailable(IPAddress.IPv6Loopback, port)
            && this.IsTcpListenerAvailable(IPAddress.Loopback, port);
    }

    private bool IsTcpListenerAvailable(IPAddress address, int port)
    {
        var tcpListeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        return tcpListeners.All(x => x.Port != port || x.Address.ToString() != address.ToString());
    }
}
