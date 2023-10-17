using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeNetworkYaml
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "leap";

    [YamlMember(Alias = "driver")]
    public string Driver { get; set; } = DockerComposeConstants.Driver.Bridge;
}