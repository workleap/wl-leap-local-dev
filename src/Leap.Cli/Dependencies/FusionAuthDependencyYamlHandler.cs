using Leap.Cli.Model;

namespace Leap.Cli.Dependencies;

internal sealed class FusionAuthDependencyYamlHandler : IDependencyYamlHandler<FusionAuthDependencyYaml>
{
    public FusionAuthDependencyYaml Merge(FusionAuthDependencyYaml leftYaml, FusionAuthDependencyYaml rightYaml)
    {
        return leftYaml;
    }

    public Dependency ToDependencyModel(FusionAuthDependencyYaml yaml)
    {
        return new FusionAuthDependency();
    }
}