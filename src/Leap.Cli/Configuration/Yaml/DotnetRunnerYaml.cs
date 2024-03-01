using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class DotnetRunnerYaml : RunnerYaml
{
    public const string YamlDiscriminator = "dotnet";

    [YamlMember(Alias = "project", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string? ProjectPath { get; set; }

    [YamlMember(Alias = "port", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? Port { get; set; }

    [YamlMember(Alias = "protocol", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Protocol { get; set; }
}