using System.IO.Abstractions;
using CliWrap;
using CliWrap.Buffered;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.DockerCompose;

internal sealed class DockerComposeManager(ICliWrap cliWrap, IFileSystem fileSystem) : IDockerComposeManager
{
    public DockerComposeYaml Configuration { get; } = new();

    public async Task EnsureDockerIsRunningAsync(CancellationToken cancellationToken)
    {
        // .NET Aspire does exactly this to ensure Docker is running
        // https://github.com/dotnet/aspire/blob/v8.0.0-preview.1.23557.2/src/Aspire.Hosting/Dcp/DcpHostService.cs#L212
        var command = new Command("docker").WithArguments(["ps", "--latest", "--quiet"]).WithValidation(CommandResultValidation.None);

        BufferedCommandResult result;
        try
        {
            result = await cliWrap.ExecuteBufferedAsync(command, cancellationToken);
        }
        catch (Exception)
        {
            throw new LeapException("Docker could not be found, please install it.");
        }

        if (result.ExitCode != 0)
        {
            throw new LeapException($"Docker was found but appears to be unhealthy. '{command.TargetFilePath} {command.Arguments}' returned {result.ExitCode}.");
        }
    }

    public async Task WriteUpdatedDockerComposeFileAsync(CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.File.Create(Constants.DockerComposeFilePath);
        await DockerComposeSerializer.SerializeAsync(stream, this.Configuration, cancellationToken);
    }

    public async Task StartDockerComposeServiceAsync(string serviceName, ILogger logger, CancellationToken cancellationToken)
    {
        var command = new Command("docker")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(Constants.DockerComposeDirectoryPath)
            .WithArguments(["compose", "--file", Constants.DockerComposeFilePath, "up", "--remove-orphans", "--wait", serviceName])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(x => logger.LogDebug("{StandardOutput}", x.Trim())))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(x => logger.LogDebug("{StandardError}", x.Trim())));

        var result = await cliWrap.ExecuteBufferedAsync(command, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"An error occurred while starting Docker Compose service '{serviceName}' with '{command.TargetFilePath} {command.Arguments}'");
        }
    }

    public async Task StopDockerComposeServiceAsync(string serviceName, ILogger logger, CancellationToken cancellationToken)
    {
        var command = new Command("docker")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(Constants.DockerComposeDirectoryPath)
            .WithArguments(["compose", "--file", Constants.DockerComposeFilePath, "stop", serviceName])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(x => logger.LogDebug("{StandardOutput}", x.Trim())))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(x => logger.LogDebug("{StandardError}", x.Trim())));

        var result = await cliWrap.ExecuteBufferedAsync(command, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"An error occurred while stopping Docker Compose service '{serviceName}' with '{command.TargetFilePath} {command.Arguments}'");
        }
    }
}