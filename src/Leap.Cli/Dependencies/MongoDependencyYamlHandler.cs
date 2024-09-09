using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyYamlHandler : IDependencyYamlHandler<MongoDependencyYaml>
{
    public MongoDependencyYaml Merge(MongoDependencyYaml leftYaml, MongoDependencyYaml rightYaml)
    {
        return leftYaml;
    }

    public Dependency ToDependencyModel(MongoDependencyYaml yaml)
    {
        return new MongoDependency();
    }
}