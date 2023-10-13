namespace Leap.Cli.DockerCompose;

internal interface IDockerComposeManager
{
    Task WriteUpdatedDockerComposeFileAsync(CancellationToken cancellationToken);
}