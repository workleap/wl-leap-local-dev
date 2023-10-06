using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeResourcesYaml
{
    [YamlMember(Alias = "limits", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public DockerComposeCpusAndMemoryYaml? Limits { get; set; }

    [YamlMember(Alias = "reservations", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public DockerComposeCpusAndMemoryYaml? Reservations { get; set; }
}