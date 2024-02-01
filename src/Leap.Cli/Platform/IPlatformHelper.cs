using System.Runtime.InteropServices;

namespace Leap.Cli.Platform;

internal interface IPlatformHelper
{
    OSPlatform CurrentOS { get; }

    Architecture ProcessArchitecture { get; }

    Task MakeExecutableAsync(string targetPath, CancellationToken cancellationToken);

    bool IsCurrentProcessElevated();

    Task StartLeapElevatedAsync(string[] args, CancellationToken cancellationToken);
}