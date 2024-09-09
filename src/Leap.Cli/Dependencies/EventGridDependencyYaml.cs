using Leap.Cli.Configuration.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Dependencies;

internal sealed class EventGridDependencyYaml : DependencyYaml
{
    public const string YamlDiscriminator = "eventgrid";

    [YamlMember(Alias = "topics", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, string?[]?>? Topics { get; set; }
}