using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal interface IDependencyYamlHandler
{
    bool CanHandle(string dependencyType);

    DependencyYaml Merge(DependencyYaml leftYaml, DependencyYaml rightYaml);

    Dependency ToDependencyModel(DependencyYaml yaml);
}