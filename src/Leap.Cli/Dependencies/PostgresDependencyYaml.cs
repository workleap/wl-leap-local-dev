using Leap.Cli.Configuration.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Dependencies;

internal sealed class PostgresDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "postgres";

    [YamlMember(Alias = "imagename", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public string? ImageName { get; set; }
}