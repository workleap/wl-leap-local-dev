using Leap.Cli.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.ProcessCompose.Yaml;

internal sealed class ProcessComposeProcessYaml
{
    [YamlMember(Alias = "command", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string Command { get; set; } = string.Empty;

    [YamlMember(Alias = "working_dir", ScalarStyle = ScalarStyle.DoubleQuoted, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? WorkingDirectory { get; set; }

    [YamlMember(Alias = "environment", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public KeyValueCollectionYaml Environment { get; set; } = new();
}