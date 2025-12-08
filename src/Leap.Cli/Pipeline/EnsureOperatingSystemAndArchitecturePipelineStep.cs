using System.Runtime.InteropServices;
using Leap.Cli.Model;
using Leap.Cli.Platform;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureOperatingSystemAndArchitecturePipelineStep : IPipelineStep
{
    private readonly IPlatformHelper _platformHelper;

    public EnsureOperatingSystemAndArchitecturePipelineStep(IPlatformHelper platformHelper)
    {
        this._platformHelper = platformHelper;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var allowedOperatingSystems = new HashSet<OSPlatform>
        {
            OSPlatform.Linux,
            OSPlatform.Windows,
            OSPlatform.OSX,
        };

        var allowedArchitectures = new HashSet<Architecture>
        {
            Architecture.X64,
            Architecture.Arm64,
        };

        if (!allowedOperatingSystems.Contains(this._platformHelper.CurrentOS))
        {
            throw new LeapException($"Unsupported operating system: {this._platformHelper.CurrentOS}");
        }

        if (!allowedArchitectures.Contains(this._platformHelper.ProcessArchitecture))
        {
            throw new LeapException($"Unsupported CPU architecture: {this._platformHelper.ProcessArchitecture}");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}