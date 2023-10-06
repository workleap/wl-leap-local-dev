namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposePortMappingYaml
{
    public DockerComposePortMappingYaml() : this(string.Empty, 0, 0)
    {
    }

    public DockerComposePortMappingYaml(int hostPort, int containerPort) : this(string.Empty, hostPort, containerPort)
    {
    }

    public DockerComposePortMappingYaml(string host, int hostPort, int containerPort)
    {
        this.Host = host;
        this.HostPort = hostPort;
        this.ContainerPort = containerPort;
    }

    public string Host { get; set; }

    public int HostPort { get; set; }

    public int ContainerPort { get; set; }
}