using Leap.Cli.Dependencies;
using Leap.Cli.DockerCompose.Yaml;

namespace Leap.Cli.Model;

internal sealed class PostgresDependency(DockerComposeImageName? imageName) : Dependency(PostgresDependencyYaml.YamlDiscriminator)
{
    public DockerComposeImageName? ImageName { get; } = imageName;
}