namespace Leap.Cli.Model;

internal sealed class MongoDependency : Dependency
{
    public required bool UseReplicaSet { get; init; }
}