using Leap.Cli.DockerCompose.Yaml;

namespace Leap.Cli.DockerCompose;

internal interface IConfigureDockerCompose
{
    void Configure(Action<DockerComposeYaml> configure);
}