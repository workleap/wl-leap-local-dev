using Leap.Cli.Configuration.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Dependencies;

internal sealed class RedisDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "redis";

    [YamlMember(Alias = "mcp", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? Mcp { get; set; } = true;
}