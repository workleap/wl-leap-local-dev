using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeVolumeYaml
{
    [YamlMember(Alias = "driver")]
    public string Driver { get; set; } = DockerComposeConstants.Driver.Local;
}