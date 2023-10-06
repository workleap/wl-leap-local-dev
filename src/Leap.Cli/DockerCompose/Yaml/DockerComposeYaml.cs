using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeYaml
{
    [YamlMember(Alias = "version", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string Version { get; set; } = DockerComposeConstants.Version3;

    [YamlMember(Alias = "services")]
    public Dictionary<string, DockerComposeServiceYaml> Services { get; set; } = new();

    [YamlMember(Alias = "volumes")]
    public Dictionary<string, DockerComposeVolumeYaml?> Volumes { get; set; } = new();

    [YamlMember(Alias = "networks", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, DockerComposeNetworkYaml?> Networks { get; set; } = new();
}