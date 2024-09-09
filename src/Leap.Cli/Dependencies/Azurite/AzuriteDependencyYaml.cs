using Leap.Cli.Configuration.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Dependencies.Azurite;

internal sealed class AzuriteDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "azurite";

    [YamlMember(Alias = "containers", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public string?[]? Containers { get; set; }

    [YamlMember(Alias = "tables", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public string?[]? Tables { get; set; }

    [YamlMember(Alias = "queues", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public string?[]? Queues { get; set; }
}