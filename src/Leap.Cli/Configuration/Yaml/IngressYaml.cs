using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class IngressYaml
{
    [YamlMember(Alias = "host", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Host { get; set; }

    [YamlMember(Alias = "path", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Path { get; set; }
}