namespace Leap.Cli.Model;

internal abstract class Binding
{
    public abstract int? Port { get; set; }
    public string? Protocol { get; set; }
}