namespace Leap.Cli.Model;

internal abstract class Runner
{
    public abstract int? Port { get; set; }

    public string? Protocol { get; set; }
}
