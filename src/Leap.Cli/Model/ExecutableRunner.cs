namespace Leap.Cli.Model;

internal sealed class ExecutableRunner : Runner
{
    public required string Command { get; init; }

    public required string[] Arguments { get; init; }

    public required string WorkingDirectory { get; init; }
}