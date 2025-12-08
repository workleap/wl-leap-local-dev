using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal abstract class DependencyYaml
{
    [YamlMember(Alias = "type", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Type { get; set; }
}