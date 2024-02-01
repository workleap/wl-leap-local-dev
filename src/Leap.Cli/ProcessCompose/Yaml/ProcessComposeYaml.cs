using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.ProcessCompose.Yaml;

internal sealed class ProcessComposeYaml
{
    [YamlMember(Alias = "is_strict")]
    public bool IsStrict { get; set; } = true;

    [YamlMember(Alias = "processes", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, ProcessComposeProcessYaml> Processes { get; set; } = new();

    [YamlMember(Alias = "log_configuration", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public ProcessComposeLogConfiguration LogConfiguration { get; set; } = new ProcessComposeLogConfiguration
    {
        DisableJson = true,
        NoColor = true,
    };
}