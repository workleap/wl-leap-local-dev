using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Model;

internal sealed class DockerRunner() : Runner(DockerRunnerYaml.YamlDiscriminator)
{
    public required string ImageAndTag { get; init; }

    public required int ContainerPort { get; init; }

    public string?[]? EnvironmentFiles { get; init; }

    public required DockerRunnerVolumeMapping[] Volumes { get; init; }
}