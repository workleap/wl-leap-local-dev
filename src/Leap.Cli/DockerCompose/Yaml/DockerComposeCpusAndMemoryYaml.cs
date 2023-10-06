using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeCpusAndMemoryYaml
{
    [YamlMember(Alias = "cpus", ScalarStyle = ScalarStyle.DoubleQuoted, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Cpus { get; set; }

    [YamlMember(Alias = "memory", ScalarStyle = ScalarStyle.DoubleQuoted, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Memory { get; set; }
}