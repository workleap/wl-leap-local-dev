using Leap.Cli.DockerCompose.Yaml;

namespace Leap.Cli.DockerCompose;

internal interface IConfigureDockerCompose
{
    Task<DockerComposeYaml?> GetExistingAsync(CancellationToken cancellationToken);

    void Configure(Action<DockerComposeYaml> configure);

    // TODO expose a method to create (or update?) a file next to the docker-compose.yml file
}