using Leap.Cli.Model;
using Leap.Cli.Yaml;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyYamlHandler : IDependencyYamlHandler
{
    public bool CanHandle(string dependencyType)
    {
        return MongoDependency.DependencyType.Equals(dependencyType, StringComparison.OrdinalIgnoreCase);
    }

    public DependencyYaml Merge(DependencyYaml leftYaml, DependencyYaml rightYaml)
    {
        return leftYaml;
    }

    public Dependency ToDependencyModel(DependencyYaml yaml)
    {
        return new MongoDependency();
    }
}