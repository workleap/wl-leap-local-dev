using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeHealthcheckYaml
{
    [YamlMember(Alias = "test", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public InlinedQuotedStringCollectionYaml? Test { get; set; }

    [YamlMember(Alias = "interval", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Interval { get; set; }

    [YamlMember(Alias = "timeout", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Timeout { get; set; }

    [YamlMember(Alias = "retries", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? Retries { get; set; }
}