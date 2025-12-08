using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class RedisDependencyYamlHandler : IDependencyYamlHandler<RedisDependencyYaml>
{
    public RedisDependencyYaml Merge(RedisDependencyYaml leftYaml, RedisDependencyYaml rightYaml)
    {
        return leftYaml;
    }

    public Dependency ToDependencyModel(RedisDependencyYaml yaml)
    {
        return new RedisDependency();
    }
}