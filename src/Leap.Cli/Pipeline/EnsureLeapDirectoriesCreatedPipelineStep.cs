using System.IO.Abstractions;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureLeapDirectoriesCreatedPipelineStep(IFileSystem fileSystem) : IPipelineStep
{
    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        fileSystem.Directory.CreateDirectory(Constants.RootDirectoryPath);
        fileSystem.Directory.CreateDirectory(Constants.GeneratedDirectoryPath);
        fileSystem.Directory.CreateDirectory(Constants.DockerComposeDirectoryPath);
        fileSystem.Directory.CreateDirectory(Constants.FusionAuthDirectoryPath);
        fileSystem.Directory.CreateDirectory(Constants.CertificatesDirectoryPath);
        fileSystem.Directory.CreateDirectory(Constants.NuGetPackagesDirectoryPath);

        try
        {
            fileSystem.Directory.Delete(Constants.DotnetExecutableDebuggingDirectoryPath, true);
        }
        catch (DirectoryNotFoundException)
        {
            // Ignored given it is expected if creating for the first time
        }

        fileSystem.Directory.CreateDirectory(Constants.DotnetExecutableDebuggingDirectoryPath);

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}