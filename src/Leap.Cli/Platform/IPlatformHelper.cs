using System.Runtime.InteropServices;

namespace Leap.Cli.Platform;

internal interface IPlatformHelper
{
    OSPlatform CurrentOS { get; }

    Architecture ProcessArchitecture { get; }

    bool IsRunningOnBuildAgent { get; }

    bool IsRunningInReleaseConfiguration { get; }

    bool IsRunningOnStableVersion { get; }

    bool IsCurrentProcessElevated { get; }

    string CurrentApplicationVersion { get; }

    Task StartLeapElevatedAsync(string[] args, CancellationToken cancellationToken);

    Task<bool> IsCodeSignedAsync(string filePath, CancellationToken cancellationToken);

    Task CodeSignBinaryAsync(string filePath, CancellationToken cancellationToken);
    bool RequiresAppleCodeSigning();
}