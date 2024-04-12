using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class FusionAuthDependencyYamlHandler : IDependencyYamlHandler
{
    public bool CanHandle(string dependencyType)
    {
        return FusionAuthDependency.DependencyType.Equals(dependencyType, StringComparison.OrdinalIgnoreCase);
    }

    public DependencyYaml Merge(DependencyYaml leftYaml, DependencyYaml rightYaml)
    {
        return leftYaml;
    }

    public Dependency ToDependencyModel(DependencyYaml yaml)
    {
        return new FusionAuthDependency();
    }
}