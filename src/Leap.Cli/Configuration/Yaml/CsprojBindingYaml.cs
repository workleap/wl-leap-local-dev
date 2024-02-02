using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class CsprojBindingYaml : BindingYaml
{
    public const string YamlDiscriminator = "csproj";

    [YamlMember(Alias = "path", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string? Path { get; set; }

    [YamlMember(Alias = "port", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? Port { get; set; }

    [YamlMember(Alias = "protocol", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Protocol { get; set; }
}