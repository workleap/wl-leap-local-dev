using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyYamlHandler : IDependencyYamlHandler<PostgresDependencyYaml>
{
    public PostgresDependencyYaml Merge(PostgresDependencyYaml leftYaml, PostgresDependencyYaml rightYaml)
    {
        return new PostgresDependencyYaml
        {
            ImageTag = leftYaml.ImageTag ?? rightYaml.ImageTag,
        };
    }

    public Dependency ToDependencyModel(PostgresDependencyYaml yaml)
    {
        return new PostgresDependency
        {
            ImageTag = yaml.ImageTag
        };
    }
}