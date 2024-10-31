using Leap.Cli.Dependencies;

namespace Leap.Cli.Model;

internal sealed class MongoDependency() : Dependency(MongoDependencyYaml.YamlDiscriminator)
{
    public required bool UseReplicaSet { get; init; }
}