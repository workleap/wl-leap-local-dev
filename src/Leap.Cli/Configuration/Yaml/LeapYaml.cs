using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class LeapYaml
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "services", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, ServiceYaml?>? Services { get; set; }

    [YamlMember(Alias = "dependencies", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public DependencyYaml?[]? Dependencies { get; set; }
}