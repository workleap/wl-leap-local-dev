namespace Leap.Cli.DockerCompose;

internal interface IDockerComposeManager : IConfigureDockerCompose
{
    Task EnsureDockerIsRunningAsync(CancellationToken cancellationToken);

    Task WriteUpdatedDockerComposeFileAsync(CancellationToken cancellationToken);

    Task StartDockerComposeAsync(CancellationToken cancellationToken);
}