using System.Diagnostics.CodeAnalysis;

namespace Leap.Cli.Platform;

internal interface IPortManager
{
    int GetRandomAvailablePort(CancellationToken cancellationToken);

    bool TryRegisterPort(int port, [NotNullWhen(false)] out InvalidPortReason? reason);

    bool IsPortInValidRange(int port);
}