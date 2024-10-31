using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeServiceYaml
{
    public DockerComposeServiceYaml()
    {
        // This is a Semgrep/AppSec recommendation to use this setting
        this.SecurityOption = ["no-new-privileges:true"];

        // Helps containers to communicate with the host machine
        this.ExtraHosts = ["host.docker.internal:host-gateway"];
    }

    [YamlMember(Alias = "image")]
    public DockerComposeImageName Image { get; set; } = DockerComposeImageName.Empty;

    [YamlMember(Alias = "container_name", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? ContainerName { get; set; }

    [YamlMember(Alias = "command", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public InlinedQuotedStringCollectionYaml Command { get; set; } = [];

    [YamlMember(Alias = "depends_on", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> DependsOn { get; set; } = [];

    [YamlMember(Alias = "security_opt", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> SecurityOption { get; set; }

    [YamlMember(Alias = "restart")]
    public string Restart { get; set; } = DockerComposeConstants.Restart.UnlessStopped;

    [YamlMember(Alias = "pull_policy")]
    public string PullPolicy { get; set; } = DockerComposeConstants.PullPolicy.Missing;

    [YamlMember(Alias = "networks", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> Networks { get; set; } = [];

    [YamlMember(Alias = "extra_hosts", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<string> ExtraHosts { get; set; }

    [YamlMember(Alias = "environment", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public KeyValueCollectionYaml Environment { get; set; } = [];

    [YamlMember(Alias = "ports", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DockerComposePortMappingYaml> Ports { get; set; } = [];

    [YamlMember(Alias = "volumes", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DockerComposeVolumeMappingYaml> Volumes { get; set; } = [];

    [YamlMember(Alias = "deploy", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public DockerComposeDeploymentYaml? Deploy { get; set; }

    [YamlMember(Alias = "healthcheck", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public DockerComposeHealthcheckYaml? Healthcheck { get; set; }
}