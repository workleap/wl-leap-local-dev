using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class OpenApiBindingYaml : BindingYaml
{
    public const string YamlDiscriminator = "openapi";

    [YamlMember(Alias = "spec", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Specification { get; set; }

    [YamlMember(Alias = "port", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? Port { get; set; }
}