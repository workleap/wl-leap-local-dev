using System.IO.Abstractions;
using CliWrap;
using CliWrap.Buffered;
using Leap.Cli.Configuration;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.DockerCompose;

internal sealed class DockerComposeManager : IDockerComposeManager
{
    private readonly ICliWrap _cliWrap;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    public DockerComposeManager(ICliWrap cliWrap, IFileSystem fileSystem, ILogger<DockerComposeManager> logger)
    {
        this._cliWrap = cliWrap;
        this._fileSystem = fileSystem;
        this._logger = logger;

        this.Configuration = new DockerComposeYaml();
    }

    public DockerComposeYaml Configuration { get; }

    public async Task EnsureDockerIsRunningAsync(CancellationToken cancellationToken)
    {
        // .NET Aspire does exactly this to ensure Docker is running
        // https://github.com/dotnet/aspire/blob/v8.0.0-preview.1.23557.2/src/Aspire.Hosting/Dcp/DcpHostService.cs#L212
        var command = new Command("docker").WithArguments(["ps", "--latest", "--quiet"]).WithValidation(CommandResultValidation.None);

        BufferedCommandResult result;
        try
        {
            result = await this._cliWrap.ExecuteBufferedAsync(command, cancellationToken);
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
        var dockerComposeFilePath = Path.Combine(Constants.DockerComposeDirectoryPath, "docker-compose.yaml");

        await using var stream = this._fileSystem.File.Create(dockerComposeFilePath);
        await DockerComposeSerializer.SerializeAsync(stream, this.Configuration, cancellationToken);
    }

    public async Task StartDockerComposeAsync(CancellationToken cancellationToken)
    {
        var command = new Command("docker")
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(Constants.DockerComposeDirectoryPath)
            .WithArguments(["compose", "up", "--pull", "missing", "--remove-orphans", "--wait"])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(x => this._logger.LogDebug("{StandardOutput}", x?.Trim())))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(x => this._logger.LogDebug("{StandardError}", x?.Trim())));

        var result = await this._cliWrap.ExecuteBufferedAsync(command, cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"An error occurred while starting Docker services with '{command.TargetFilePath} {command.Arguments}'");
        }
    }
}