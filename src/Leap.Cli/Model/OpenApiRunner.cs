namespace Leap.Cli.Model;

internal sealed class OpenApiRunner : Runner
{
    public required string Specification { get; init; }

    public required bool IsUrl { get; init; }
}