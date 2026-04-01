using Leap.Cli.Configuration.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "postgres";

    [YamlMember(Alias = "imagetag", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? ImageTag { get; set; }

    [YamlMember(Alias = "mcp", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public bool? Mcp { get; set; }
}