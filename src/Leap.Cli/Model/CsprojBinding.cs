namespace Leap.Cli.Model;

internal sealed class CsprojBinding : Binding
{
    public string Path { get; set; } = string.Empty;

    public override int? Port { get; set; }
}