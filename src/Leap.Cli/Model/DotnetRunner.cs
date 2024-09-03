namespace Leap.Cli.Model;

internal sealed class DotnetRunner : Runner
{
    public required string ProjectPath { get; init; }

    public required bool Watch { get; init; }
}