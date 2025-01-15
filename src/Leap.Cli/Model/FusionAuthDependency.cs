using Leap.Cli.Dependencies;

namespace Leap.Cli.Model;

internal sealed class FusionAuthDependency() : Dependency(FusionAuthDependencyYaml.YamlDiscriminator)
{
}