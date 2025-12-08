namespace Leap.Cli.Platform;

internal interface IPortManager
{
    int GetRandomAvailablePort(CancellationToken cancellationToken);

    bool IsPortInValidRange(int port);

    bool IsPortAvailable(int port);
}