using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Model;

internal sealed class RemoteRunner() : Runner(RemoteRunnerYaml.YamlDiscriminator)
{
    public required string Url { get; init; }
}