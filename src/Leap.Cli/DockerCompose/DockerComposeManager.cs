using System.IO.Abstractions;
using Leap.Cli.Configuration;
using Leap.Cli.DockerCompose.Yaml;

namespace Leap.Cli.DockerCompose;

internal sealed class DockerComposeManager : IConfigureDockerCompose, IDockerComposeManager
{
    private readonly IFileSystem _fileSystem;
    private readonly List<Action<DockerComposeYaml>> _configurations;

    public DockerComposeManager(IFileSystem fileSystem)
    {
        this._fileSystem = fileSystem;
        this._configurations = new List<Action<DockerComposeYaml>>();
    }

    public void Configure(Action<DockerComposeYaml> configure)
    {
        this._configurations.Add(configure);
    }

    public async Task WriteUpdatedDockerComposeFileAsync(CancellationToken cancellationToken)
    {
        var dockerComposeYaml = new DockerComposeYaml();

        foreach (var configuration in this._configurations)
        {
            configuration(dockerComposeYaml);
        }

        var dockerComposeFilePath = Path.Combine(ConfigurationConstants.GeneratedDirectoryPath, "docker-compose.yml");

        await using var stream = this._fileSystem.File.Create(dockerComposeFilePath);
        await DockerComposeSerializer.SerializeAsync(stream, dockerComposeYaml, cancellationToken);
    }
}