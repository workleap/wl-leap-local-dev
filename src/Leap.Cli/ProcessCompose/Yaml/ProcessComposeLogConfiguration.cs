using YamlDotNet.Serialization;

namespace Leap.Cli.ProcessCompose.Yaml;

internal sealed class ProcessComposeLogConfiguration
{
    [YamlMember(Alias = "no_color")]
    public bool NoColor { get; set; }

    [YamlMember(Alias = "disable_json")]
    public bool DisableJson { get; set; }
}