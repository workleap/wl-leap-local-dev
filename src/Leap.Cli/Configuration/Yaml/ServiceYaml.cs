using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class ServiceYaml
{
    [YamlMember(Alias = "ingress", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public IngressYaml? Ingress { get; set; }

    [YamlMember(Alias = "bindings", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public BindingYaml?[]? Bindings { get; set; }
}