namespace Leap.Cli.Model;

internal sealed class Ingress
{
    public const string DefaultPath = "/";

    public IngressHost Host { get; set; } = IngressHost.Localhost;

    public int LocalhostPort { get; set; }

    public string Path { get; set; } = DefaultPath;
}