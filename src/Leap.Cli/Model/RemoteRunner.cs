namespace Leap.Cli.Model;

internal sealed class RemoteRunner : Runner
{
    public string Url { get; set; } = string.Empty;

    public override int? Port { get; set; }
}