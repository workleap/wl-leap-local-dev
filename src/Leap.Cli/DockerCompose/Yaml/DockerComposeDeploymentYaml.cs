using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeDeploymentYaml
{
    [YamlMember(Alias = "resources", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public DockerComposeResourcesYaml? Resources { get; set; }
}