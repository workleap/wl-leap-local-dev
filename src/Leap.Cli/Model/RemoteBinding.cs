namespace Leap.Cli.Model;

internal sealed class RemoteBinding : Binding
{
    public string Url { get; set; } = string.Empty;

    public override int? Port { get; set; }
}