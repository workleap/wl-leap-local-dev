using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Model;

internal sealed class DotnetRunner() : Runner(DotnetRunnerYaml.YamlDiscriminator)
{
    public required string ProjectPath { get; init; }

    public required bool Watch { get; init; }
}