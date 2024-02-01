using Leap.Cli.DockerCompose.Yaml;

namespace Leap.Cli.DockerCompose;

internal interface IConfigureDockerCompose
{
    DockerComposeYaml Configuration { get; }
}