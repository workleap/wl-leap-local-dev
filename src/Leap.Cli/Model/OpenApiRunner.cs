using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Model;

internal sealed class OpenApiRunner() : Runner(OpenApiRunnerYaml.YamlDiscriminator)
{
    public required string Specification { get; init; }

    public required bool IsUrl { get; init; }
}