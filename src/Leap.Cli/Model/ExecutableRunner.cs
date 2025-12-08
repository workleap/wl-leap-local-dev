using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Model;

internal sealed class ExecutableRunner() : Runner(ExecutableRunnerYaml.YamlDiscriminator)
{
    public required string Command { get; init; }

    public required string[] Arguments { get; init; }

    public required string WorkingDirectory { get; init; }
}