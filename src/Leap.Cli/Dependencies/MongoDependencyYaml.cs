using Leap.Cli.Configuration.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Dependencies;

internal sealed class MongoDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "mongo";

    [YamlMember(Alias = "replset", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? UseReplicaSet { get; set; }

    [YamlMember(Alias = "mcp", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? Mcp { get; set; } = true;
}