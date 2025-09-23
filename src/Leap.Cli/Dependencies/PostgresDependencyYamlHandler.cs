using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyYamlHandler : IDependencyYamlHandler<PostgresDependencyYaml>
{
    public PostgresDependencyYaml Merge(PostgresDependencyYaml leftYaml, PostgresDependencyYaml rightYaml)
    {
        return leftYaml;
    }

    public Dependency ToDependencyModel(PostgresDependencyYaml yaml)
    {
        var imageName = yaml.ImageName is not null ? new DockerComposeImageName(yaml.ImageName) : null;
        return new PostgresDependency(imageName);
    }
}