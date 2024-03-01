using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal abstract class RunnerYaml
{
    // TODO forward the environment variables to the application state
    [YamlMember(Alias = "env", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public KeyValueCollectionYaml? EnvironmentVariables { get; set; }
}