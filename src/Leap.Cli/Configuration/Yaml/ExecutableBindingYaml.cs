using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class ExecutableBindingYaml : BindingYaml
{
    public const string YamlDiscriminator = "executable";

    [YamlMember(Alias = "command", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public string? Command { get; set; }

    [YamlMember(Alias = "args", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public string?[]? Arguments { get; set; }

    [YamlMember(Alias = "workingDirectory", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string? WorkingDirectory { get; set; }

    [YamlMember(Alias = "port", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? Port { get; set; }

    [YamlMember(Alias = "protocol", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Protocol { get; set; }
}