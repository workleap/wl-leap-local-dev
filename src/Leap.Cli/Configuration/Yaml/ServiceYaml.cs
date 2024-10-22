using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class ServiceYaml
{
    [YamlMember(Alias = "ingress", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public IngressYaml? Ingress { get; set; }

    [YamlMember(Alias = "runners", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public RunnerYaml?[]? Runners { get; set; }

    [YamlMember(Alias = "env", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public KeyValueCollectionYaml? EnvironmentVariables { get; set; }

    [YamlMember(Alias = "profiles", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public string?[]? Profiles { get; set; }
}