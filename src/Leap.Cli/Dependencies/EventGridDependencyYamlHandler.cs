using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class EventGridDependencyYamlHandler : IDependencyYamlHandler
{
    public bool CanHandle(string dependencyType)
    {
        return EventGridDependency.DependencyType.Equals(dependencyType, StringComparison.OrdinalIgnoreCase);
    }

    public DependencyYaml Merge(DependencyYaml leftYaml, DependencyYaml rightYaml) => leftYaml;

    public Dependency ToDependencyModel(DependencyYaml yaml) => new EventGridDependency();
}
