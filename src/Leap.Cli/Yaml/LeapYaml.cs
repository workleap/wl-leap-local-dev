using YamlDotNet.Serialization;

namespace Leap.Cli.Yaml;

internal sealed class LeapYaml
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "dependencies", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DependencyYaml> Dependencies { get; set; } = new();
}