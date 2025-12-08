using Leap.Cli.Configuration;
using Leap.Cli.Model;
using Leap.Cli.Platform;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureMkcertCertificateExistsPipelineStep(
    MkcertCertificateManager mkcertCertificateManager,
    LeapConfigManager leapConfigManager)
    : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (leapConfigManager.RemoteEnvironmentName is not null)
        {
            return;
        }

        await mkcertCertificateManager.EnsureCertificateIsInstalledInLeapDirectoryAsync(cancellationToken);
        await mkcertCertificateManager.EnsureCertificateAuthorityIsInstalledInComputerRootStoreAsync(cancellationToken);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}