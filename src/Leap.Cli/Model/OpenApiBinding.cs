namespace Leap.Cli.Model;

internal sealed class OpenApiBinding : Binding
{
    public string Specification { get; set; } = string.Empty;

    public override int? Port { get; set; }
}