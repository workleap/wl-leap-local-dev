using Leap.Cli.Model;
using Leap.Cli.Platform;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureMkcertCertificateExistsPipelineStep(MkcertCertificateManager mkcertCertificateManager)
    : IPipelineStep
{
    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        await mkcertCertificateManager.EnsureCertificateIsInstalledInLeapDirectoryAsync(cancellationToken);
        await mkcertCertificateManager.EnsureCertificateAuthorityIsInstalledInComputerRootStoreAsync(cancellationToken);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
