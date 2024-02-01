namespace Leap.Cli.Model;

internal sealed class Ingress
{
    public string? Host { get; set; }

    public int? InternalPort { get; set; }

    public int? ExternalPort { get; set; }

    public string? Path { get; set; }
}