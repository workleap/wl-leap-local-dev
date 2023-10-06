using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeServiceYaml
{
    [YamlMember(Alias = "image")]
    public string Image { get; set; } = string.Empty;

    [YamlMember(Alias = "command")]
    public string Command { get; set; } = string.Empty;

    [YamlMember(Alias = "depends_on", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> DependsOn { get; set; } = new();

    [YamlMember(Alias = "security_opt", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> SecurityOption { get; set; } = new();

    [YamlMember(Alias = "restart")]
    public string Restart { get; set; } = DockerComposeConstants.Restart.UnlessStopped;

    [YamlMember(Alias = "networks", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> Networks { get; set; } = new();

    [YamlMember(Alias = "extra_hosts", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> ExtraHosts { get; set; } = new();

    [YamlMember(Alias = "environment", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public KeyValueCollectionYaml Environment { get; set; } = new();

    [YamlMember(Alias = "ports", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DockerComposePortMappingYaml> Ports { get; set; } = new();

    [YamlMember(Alias = "volumes", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DockerComposeVolumeMappingYaml> Volumes { get; set; } = new();

    [YamlMember(Alias = "deploy", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public DockerComposeDeploymentYaml? Deploy { get; set; }
}