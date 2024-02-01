namespace Leap.Cli.ProcessCompose;

internal interface IProcessComposeManager : IConfigureProcessCompose
{
    Task EnsureProcessComposeExecutableExistsAsync(CancellationToken cancellationToken);

    Task WriteUpdatedProcessComposeFileAsync(CancellationToken cancellationToken);

    Task StartProcessComposeAsync(CancellationToken cancellationToken);
}