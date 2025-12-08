using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal interface IDependencyYamlHandler<TYaml>
    where TYaml : DependencyYaml, new()
{
    TYaml Merge(TYaml leftYaml, TYaml rightYaml);

    Dependency ToDependencyModel(TYaml yaml);
}