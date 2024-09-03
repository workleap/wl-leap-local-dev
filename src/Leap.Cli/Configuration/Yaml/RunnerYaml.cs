using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal abstract class RunnerYaml
{
    [YamlMember(Alias = "env", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public KeyValueCollectionYaml? EnvironmentVariables { get; set; }
}