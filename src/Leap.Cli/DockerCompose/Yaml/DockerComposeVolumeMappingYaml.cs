namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeVolumeMappingYaml
{
    public DockerComposeVolumeMappingYaml()
    {
    }

    public DockerComposeVolumeMappingYaml(string source, string destination)
    {
        this.Source = source;
        this.Destination = destination;
    }

    public DockerComposeVolumeMappingYaml(string source, string destination, string mode)
    {
        this.Source = source;
        this.Destination = destination;
        this.Mode = mode;
    }

    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public string Mode { get; set; } = DockerComposeConstants.Volume.ReadWrite;
}