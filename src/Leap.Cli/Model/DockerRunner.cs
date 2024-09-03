namespace Leap.Cli.Model;

internal sealed class DockerRunner : Runner
{
    public required string ImageAndTag { get; init; }

    public required int ContainerPort { get; init; }

    public required DockerRunnerVolumeMapping[] Volumes { get; init; }
}