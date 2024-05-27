namespace Leap.Cli.Model;

internal sealed class DotnetRunner : Runner
{
    public string ProjectPath { get; set; } = string.Empty;

    public override int? Port { get; set; }

    public bool Watch { get; set; }
}