using Leap.Cli.Yaml;

namespace Leap.Cli.Dependencies;

internal interface IDependencyYamlHandler
{
    bool CanHandle(string dependencyType);

    DependencyYaml Merge(DependencyYaml leftYaml, DependencyYaml rightYaml);

    Model.Dependency ToDependencyModel(DependencyYaml yaml);
}