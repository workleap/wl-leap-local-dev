namespace Leap.Cli.Model;

internal sealed class ExecutableBinding : Binding
{
    public string Command { get; set; } = string.Empty;

    public string[] Arguments { get; set; } = Array.Empty<string>();

    public string WorkingDirectory { get; set; } = string.Empty;

    public override int? Port { get; set; }
}