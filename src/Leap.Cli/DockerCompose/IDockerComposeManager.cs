using Microsoft.Extensions.Logging;

namespace Leap.Cli.DockerCompose;

internal interface IDockerComposeManager : IConfigureDockerCompose
{
    Task EnsureDockerIsRunningAsync(CancellationToken cancellationToken);

    Task WriteUpdatedDockerComposeFileAsync(CancellationToken cancellationToken);

    Task StartDockerComposeServiceAsync(string serviceName, ILogger logger, CancellationToken cancellationToken);

    Task StopDockerComposeServiceAsync(string serviceName, ILogger logger, CancellationToken cancellationToken);
}