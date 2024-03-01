namespace Leap.Cli.Model;

internal sealed class DockerRunner : Runner
{
    public string Image { get; set; } = string.Empty;

    public int ContainerPort { get; set; }

    public int? HostPort { get; set; }

    public override int? Port
    {
        get => this.HostPort;
        set => this.HostPort = value;
    }
}