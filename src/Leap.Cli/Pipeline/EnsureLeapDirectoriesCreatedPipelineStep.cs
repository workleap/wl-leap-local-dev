using System.IO.Abstractions;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureLeapDirectoriesCreatedPipelineStep : IPipelineStep
{
    private readonly IFileSystem _fileSystem;

    public EnsureLeapDirectoriesCreatedPipelineStep(IFileSystem fileSystem)
    {
        this._fileSystem = fileSystem;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        this._fileSystem.Directory.CreateDirectory(Constants.RootDirectoryPath);
        this._fileSystem.Directory.CreateDirectory(Constants.GeneratedDirectoryPath);
        this._fileSystem.Directory.CreateDirectory(Constants.DockerComposeDirectoryPath);
        this._fileSystem.Directory.CreateDirectory(Constants.CertificatesDirectoryPath);

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}