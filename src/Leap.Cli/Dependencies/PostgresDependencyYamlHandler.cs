using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyYamlHandler : IDependencyYamlHandler<PostgresDependencyYaml>
{
    public PostgresDependencyYaml Merge(PostgresDependencyYaml leftYaml, PostgresDependencyYaml rightYaml)
    {
        return new PostgresDependencyYaml
        {
            ImageName = leftYaml.ImageName ?? rightYaml.ImageName,
        };
    }

    public Dependency ToDependencyModel(PostgresDependencyYaml yaml)
    {
        return new PostgresDependency
        {
            ImageName = yaml.ImageName
        };
    }
}