using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeServiceYaml
{
    public DockerComposeServiceYaml()
    {
        // This is a Semgrep recommendation to use this setting
        this.SecurityOption = new List<string>
        {
            "no-new-privileges:true",
        };

        // Helps containers to communicate with the host machine
        this.ExtraHosts = new List<string>
        {
            "host.docker.internal:host-gateway",
        };
    }

    [YamlMember(Alias = "image")]
    public string Image { get; set; } = string.Empty;

    [YamlMember(Alias = "container_name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? ContainerName { get; set; }

    [YamlMember(Alias = "command", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public DockerComposeCommandYaml Command { get; set; } = new();

    [YamlMember(Alias = "depends_on", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> DependsOn { get; set; } = new();

    [YamlMember(Alias = "security_opt", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> SecurityOption { get; set; }

    [YamlMember(Alias = "restart")]
    public string Restart { get; set; } = DockerComposeConstants.Restart.UnlessStopped;

    [YamlMember(Alias = "networks", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> Networks { get; set; } = new();

    [YamlMember(Alias = "extra_hosts", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> ExtraHosts { get; set; }

    [YamlMember(Alias = "environment", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public KeyValueCollectionYaml Environment { get; set; } = new();

    [YamlMember(Alias = "ports", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DockerComposePortMappingYaml> Ports { get; set; } = new();

    [YamlMember(Alias = "volumes", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DockerComposeVolumeMappingYaml> Volumes { get; set; } = new();

    [YamlMember(Alias = "deploy", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public DockerComposeDeploymentYaml? Deploy { get; set; }
}