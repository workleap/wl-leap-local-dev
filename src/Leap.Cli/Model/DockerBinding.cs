namespace Leap.Cli.Model;

// TODO docker bindings need to be shutdown when Leap is stopped
internal sealed class DockerBinding : Binding
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