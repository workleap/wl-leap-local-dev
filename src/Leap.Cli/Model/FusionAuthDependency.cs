using Leap.Cli.Dependencies;
using Leap.Cli.Model.Traits;

namespace Leap.Cli.Model;

internal sealed class FusionAuthDependency() : Dependency(FusionAuthDependencyYaml.YamlDiscriminator), IRequireAzCLIProxy
{
}