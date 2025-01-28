using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeYaml
{
    public DockerComposeYaml()
    {
        this.Networks = new Dictionary<string, DockerComposeNetworkYaml?>
        {
            [DockerComposeConstants.LeapNetworkName] = new DockerComposeNetworkYaml
            {
                Name = DockerComposeConstants.LeapNetworkName,
                Driver = DockerComposeConstants.Driver.Bridge,
            },
        };
    }

    [YamlMember(Alias = "name", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string Name { get; set; } = DockerComposeConstants.LeapProjectName;

    [YamlMember(Alias = "services", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, DockerComposeServiceYaml> Services { get; set; } = new();

    [YamlMember(Alias = "volumes", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, DockerComposeVolumeYaml?> Volumes { get; set; } = new();

    [YamlMember(Alias = "networks", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, DockerComposeNetworkYaml?> Networks { get; set; }
}