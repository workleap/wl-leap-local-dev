using System.IO.Abstractions;
using Leap.Cli.Configuration;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureLeapDirectoryCreatedPipelineStep : IPipelineStep
{
    private readonly IFileSystem _fileSystem;

    public EnsureLeapDirectoryCreatedPipelineStep(IFileSystem fileSystem)
    {
        this._fileSystem = fileSystem;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        this._fileSystem.Directory.CreateDirectory(ConfigurationConstants.RootDirectoryPath);
        this._fileSystem.Directory.CreateDirectory(ConfigurationConstants.GeneratedDirectoryPath);

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}