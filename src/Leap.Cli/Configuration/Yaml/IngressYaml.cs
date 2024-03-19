using Leap.Cli.Model.Traits;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class IngressYaml : IHasPort
{
    [YamlMember(Alias = "host", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Host { get; set; }

    [YamlMember(Alias = "port", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? Port { get; set; }

    [YamlMember(Alias = "path", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Path { get; set; }
}