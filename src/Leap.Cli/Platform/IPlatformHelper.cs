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

    string? DotnetRootPath { get; }

    Task StartLeapElevatedAsync(string[] args, CancellationToken cancellationToken);
}