using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class RemoteBindingYaml : BindingYaml
{
    public const string YamlDiscriminator = "remote";

    [YamlMember(Alias = "url", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string? Url { get; set; }
}