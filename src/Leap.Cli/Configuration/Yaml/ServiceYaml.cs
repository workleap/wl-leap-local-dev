using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class ServiceYaml
{
    [YamlMember(Alias = "ingress", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public IngressYaml? Ingress { get; set; }

    [YamlMember(Alias = "runners", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public RunnerYaml?[]? Runners { get; set; }
}