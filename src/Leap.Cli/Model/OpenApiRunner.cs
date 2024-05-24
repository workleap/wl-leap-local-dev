namespace Leap.Cli.Model;

internal sealed class OpenApiRunner : Runner
{
    public string Specification { get; set; } = string.Empty;

    public bool IsUrl { get; set; }

    public override int? Port { get; set; }
}